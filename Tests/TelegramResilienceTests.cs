namespace ElRoso.Commons.Tests;

using System.Net;
using System.Net.Http;
using ElRoso.DependencyInjection;
using ElRoso.Exceptions;
using ElRoso.Services;
using ElRoso.Telegram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

/// <summary>
/// EN: Telegram, like any external service, refuses now and then — a 429 when the bot sends too fast,
///     a 502 while they deploy. Nothing retried anything: the first refusal lost the message, and the
///     caller found out through an exception it could only log.
///     🔴 What is NOT retried matters as much: a send that timed out never repeats, because
///     sendMessage is not idempotent and "it never arrived" is indistinguishable from "it arrived and
///     the answer got lost".
/// ES: Telegram, como todo servicio externo, rechaza cada tanto — un 429 cuando el bot manda
///     demasiado rápido, un 502 mientras despliegan. Nada reintentaba nada: el primer rechazo perdía
///     el mensaje, y quien llamaba se enteraba por una excepción que solo podía loguear.
///     🔴 Lo que NO se reintenta importa igual: un envío que expiró no se repite, porque sendMessage
///     no es idempotente y "no llegó nunca" no se distingue de "llegó y se perdió la respuesta".
/// </summary>
public class TelegramResilienceTests
{
    private const string BotName = "alertas";

    private static (ITelegramBot Bot, IHttpClientFactory Clients, Func<int> Attempts) CreateBot(
        params HttpStatusCode[] answers)
    {
        var attempts = 0;
        var handler = new Mock<HttpMessageHandler>();

        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((_, _) =>
            {
                var index = Interlocked.Increment(ref attempts) - 1;
                var status = index < answers.Length ? answers[index] : answers[^1];

                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent("""{"ok":true,"result":[]}""", System.Text.Encoding.UTF8, "application/json"),
                });
            });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelegramBot(
            BotName,
            o => o.Token = "123:fake-token",
            // Esperas mínimas: acá se prueba cuántas veces insiste, no cuánto tarda.
            r =>
            {
                r.BaseDelay = TimeSpan.FromMilliseconds(1);
                r.MaxRetryAttempts = 2;
            });

        services.AddHttpClient(TelegramClientNames.Send).ConfigurePrimaryHttpMessageHandler(() => handler.Object);
        services.AddHttpClient(TelegramClientNames.Polling).ConfigurePrimaryHttpMessageHandler(() => handler.Object);

        var provider = services.BuildServiceProvider();

        return (
            provider.GetRequiredService<ITelegramBotFactory>().Get(BotName),
            provider.GetRequiredService<IHttpClientFactory>(),
            () => attempts);
    }

    [Fact]
    public async Task ASendRefusedWithA429_IsRetriedAndGoesThrough()
    {
        var (bot, _, attempts) = CreateBot(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);

        await bot.SendMessageAsync(9, "hola");

        Assert.Equal(2, attempts());
    }

    [Fact]
    public async Task ASendRefusedWithA5xx_IsRetriedAndGoesThrough()
    {
        var (bot, _, attempts) = CreateBot(HttpStatusCode.BadGateway, HttpStatusCode.OK);

        await bot.SendMessageAsync(9, "hola");

        Assert.Equal(2, attempts());
    }

    [Fact]
    public async Task ASendRejectedWithA400_IsNotRetried()
    {
        // EN: A malformed message is not going to become well-formed on the second try; insisting
        //     only burns the rate limit that the retries are there to protect.
        // ES: Un mensaje mal armado no se va a arreglar en el segundo intento; insistir solo quema el
        //     límite de tasa que los reintentos están para cuidar.
        var (bot, _, attempts) = CreateBot(HttpStatusCode.BadRequest);

        await Assert.ThrowsAsync<HttpRequestException>(() => bot.SendMessageAsync(9, "hola"));

        Assert.Equal(1, attempts());
    }

    [Fact]
    public async Task AThrottleThatDoesNotLetUp_EndsAsARateLimitException()
    {
        // EN: Retrying absorbs the momentary throttle. A sustained one has to reach the caller, with
        //     the wait Telegram asked for — swallowing it would drop the message in silence.
        // ES: El reintento absorbe el freno momentáneo. El sostenido tiene que llegar a quien llamó,
        //     con la espera que pidió Telegram — tragárselo perdería el mensaje en silencio.
        var (bot, _, attempts) = CreateBot(HttpStatusCode.TooManyRequests);

        await Assert.ThrowsAsync<TelegramRateLimitException>(() => bot.SendMessageAsync(9, "hola"));

        Assert.Equal(3, attempts());
    }

    [Fact]
    public async Task ThePollingClientDoesNotRetry()
    {
        // EN: The polling loop is its own retry — it comes back every 25 seconds forever, and each
        //     bot already waits 5 seconds after a failure. Retrying inside a long poll stacks a
        //     second loop on top of that one and delays the messages it was supposed to protect.
        // ES: El loop de polling es su propio reintento — vuelve cada 25 segundos para siempre, y
        //     cada bot ya espera 5 segundos tras una falla. Reintentar adentro de un long poll apila
        //     un segundo loop sobre ese y demora los mensajes que venía a proteger.
        var (bot, _, attempts) = CreateBot(HttpStatusCode.BadGateway);

        await Assert.ThrowsAsync<HttpRequestException>(() => bot.GetUpdatesAsync(timeoutSeconds: 25));

        Assert.Equal(1, attempts());
    }

    [Fact]
    public void ThePollingClientOutlivesTheLongPoll()
    {
        // EN: 🔴 The trap this whole split exists for: the resilience handler cuts each attempt at 15
        //     seconds, and a long poll waits 25 by design. Sharing one client would kill every poll
        //     mid-wait — the polling would look "slow" and nobody would connect it to the retries.
        // ES: 🔴 La trampa por la que existe toda esta división: el handler de resiliencia corta cada
        //     intento a los 15 segundos, y un long poll espera 25 a propósito. Compartir un cliente
        //     mataría cada poll en plena espera — el polling se vería "lento" y nadie lo ataría a los
        //     reintentos.
        var (_, clients, _) = CreateBot(HttpStatusCode.OK);

        var polling = clients.CreateClient(TelegramClientNames.Polling);

        Assert.True(polling.Timeout >= TimeSpan.FromSeconds(60), $"El cliente de polling expira a los {polling.Timeout.TotalSeconds}s.");
    }

    [Fact]
    public async Task ThePollingClientDoesNotLogTheTokenEither()
    {
        // EN: The token travels IN the URI, so the polling client needs the same treatment the
        //     sending one got — a second client is a second way to leak the same credential.
        // ES: El token viaja EN la URI, así que el cliente de polling necesita el mismo trato que el
        //     de envío — un segundo cliente es una segunda forma de filtrar la misma credencial.
        var capture = new CapturingLoggerProvider();

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"result":[]}""", System.Text.Encoding.UTF8, "application/json"),
            });

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture).SetMinimumLevel(LogLevel.Trace));
        services.AddTelegramBot(BotName, o => o.Token = "123:fake-token");
        services.AddHttpClient(TelegramClientNames.Polling).ConfigurePrimaryHttpMessageHandler(() => handler.Object);

        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<ITelegramBotFactory>().Get(BotName).GetUpdatesAsync();

        Assert.DoesNotContain("fake-token", string.Join("\n", capture.Lines), StringComparison.Ordinal);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Lines { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this.Lines);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (lines)
                {
                    lines.Add(formatter(state, exception));
                }
            }
        }
    }
}
