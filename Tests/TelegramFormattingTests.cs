namespace ElRoso.Commons.Tests;

using System.Net;
using System.Net.Http;
using System.Text.Json;
using ElRoso.DependencyInjection;
using ElRoso.Exceptions;
using ElRoso.Services;
using ElRoso.Telegram;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;

/// <summary>
/// EN: Until now a bot could only send plain text with no buttons, and a 429 — Telegram refusing
///     because the bot sends too fast — surfaced as a generic HttpRequestException that said nothing
///     about waiting.
/// ES: Hasta ahora un bot solo podía mandar texto plano sin botones, y un 429 — Telegram rechazando
///     porque el bot manda demasiado rápido — aparecía como un HttpRequestException genérico que no
///     decía nada de esperar.
/// </summary>
public class TelegramFormattingTests
{
    private const string BotName = "alertas";

    private static (ITelegramBot Bot, List<string> Bodies) CreateBot(
        HttpStatusCode status = HttpStatusCode.OK,
        string payload = """{"ok":true,"result":{"message_id":1}}""",
        IEnumerable<KeyValuePair<string, string>>? headers = null)
    {
        var bodies = new List<string>();
        var handler = new Mock<HttpMessageHandler>();

        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, ct) =>
            {
                if (request.Content is not null)
                {
                    bodies.Add(await request.Content.ReadAsStringAsync(ct));
                }

                var response = new HttpResponseMessage(status)
                {
                    Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
                };

                foreach (var header in headers ?? [])
                {
                    response.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                return response;
            });

        var services = new ServiceCollection();
        services.AddLogging();
        // EN: No retries here — this class checks what the request carries and what the failure
        //     says, not how many times the client insists. That is TelegramResilienceTests.
        // ES: Sin reintentos acá — esta clase mira qué lleva el request y qué dice la falla, no
        //     cuántas veces insiste el cliente. Eso es TelegramResilienceTests.
        services.AddTelegramBot(BotName, o => o.Token = "123:fake-token", r => r.MaxRetryAttempts = 0);
        services.AddHttpClient(TelegramClientNames.Send).ConfigurePrimaryHttpMessageHandler(() => handler.Object);

        var provider = services.BuildServiceProvider();

        return (provider.GetRequiredService<ITelegramBotFactory>().Get(BotName), bodies);
    }

    private static JsonElement Payload(List<string> bodies) => JsonDocument.Parse(bodies.Single()).RootElement;

    [Fact]
    public async Task SendMessageAsync_WithoutOptions_StaysPlainText()
    {
        // EN: The old call has to keep producing the old request, byte for byte in what matters: no
        //     parse_mode, no reply_markup. Formatting is opt-in.
        // ES: La llamada vieja tiene que seguir produciendo el request viejo en lo que importa: sin
        //     parse_mode, sin reply_markup. El formato es opt-in.
        var (bot, bodies) = CreateBot();

        await bot.SendMessageAsync(9, "hola");

        var payload = Payload(bodies);
        Assert.False(payload.TryGetProperty("parse_mode", out _));
        Assert.False(payload.TryGetProperty("reply_markup", out _));
    }

    [Fact]
    public async Task SendMessageAsync_WithAParseMode_TellsTelegramHowToReadTheText()
    {
        var (bot, bodies) = CreateBot();

        await bot.SendMessageAsync(9, "*negrita*", new TelegramSendOptions { ParseMode = TelegramParseMode.MarkdownV2 });

        Assert.Equal("MarkdownV2", Payload(bodies).GetProperty("parse_mode").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_WithAKeyboard_SendsTheButtonsAsRows()
    {
        var keyboard = new TelegramInlineKeyboard()
            .AddRow(TelegramInlineButton.Callback("Aprobar", "ok:7"), TelegramInlineButton.Callback("Rechazar", "no:7"))
            .AddRow(TelegramInlineButton.Link("Ver factura", "https://elroso.ar/f/7"));

        var (bot, bodies) = CreateBot();

        await bot.SendMessageAsync(9, "Factura 7", new TelegramSendOptions { InlineKeyboard = keyboard });

        var rows = Payload(bodies).GetProperty("reply_markup").GetProperty("inline_keyboard");
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal(2, rows[0].GetArrayLength());
        Assert.Equal("ok:7", rows[0][0].GetProperty("callback_data").GetString());
        Assert.Equal("https://elroso.ar/f/7", rows[1][0].GetProperty("url").GetString());

        // EN: A callback button must not carry a url key, and the other way around — Telegram rejects
        //     a button that has both. ES: Un botón de callback no debe llevar url, ni al revés —
        //     Telegram rechaza el que tiene los dos.
        Assert.False(rows[0][0].TryGetProperty("url", out _));
        Assert.False(rows[1][0].TryGetProperty("callback_data", out _));
    }

    [Fact]
    public async Task SendPhotoAsync_AndSendDocumentAsync_AlsoTakeOptions()
    {
        var (photoBot, photoBodies) = CreateBot();
        await photoBot.SendPhotoAsync(9, "https://elroso.ar/f.png", "pie", new TelegramSendOptions { ParseMode = TelegramParseMode.Html });
        Assert.Equal("HTML", Payload(photoBodies).GetProperty("parse_mode").GetString());

        var (docBot, docBodies) = CreateBot();
        await docBot.SendDocumentAsync(9, "https://elroso.ar/f.pdf", "pie", new TelegramSendOptions { DisableNotification = true });
        Assert.True(Payload(docBodies).GetProperty("disable_notification").GetBoolean());
    }

    [Fact]
    public void CallbackButton_LongerThanTelegramAccepts_FailsWhereItIsWritten()
    {
        // EN: Telegram caps callback data at 64 bytes and answers 400. Failing at construction names
        //     the button; failing at send time names nothing.
        // ES: Telegram limita el callback a 64 bytes y responde 400. Fallar al construirlo nombra el
        //     botón; fallar al enviar no nombra nada.
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => TelegramInlineButton.Callback("Aprobar", new string('x', 65)));

        Assert.Contains("Aprobar", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendMessageAsync_WhenTelegramThrottles_SaysHowLongToWait()
    {
        // EN: Telegram limits a bot to ~30 messages per second and answers 429 with the wait in the
        //     body, and usually in a Retry-After header too. That number was being thrown away: the
        //     caller got an HttpRequestException that said "429" and nothing else, so the only
        //     available reaction was to retry immediately — which makes the block longer.
        // ES: Telegram limita a un bot a ~30 mensajes por segundo y responde 429 con la espera en el
        //     cuerpo, y normalmente también en un header Retry-After. Ese número se tiraba: quien
        //     llamaba recibía un HttpRequestException que decía "429" y nada más, así que la única
        //     reacción posible era reintentar ya mismo — que alarga el bloqueo.
        var (bot, _) = CreateBot(
            HttpStatusCode.TooManyRequests,
            """{"ok":false,"error_code":429,"description":"Too Many Requests: retry after 7","parameters":{"retry_after":7}}""");

        var exception = await Assert.ThrowsAsync<TelegramRateLimitException>(() => bot.SendMessageAsync(9, "hola"));

        Assert.Equal(TimeSpan.FromSeconds(7), exception.RetryAfter);
        Assert.Equal(BotName, exception.BotName);
    }

    [Fact]
    public async Task SendMessageAsync_WhenThrottledWithoutABody_FallsBackToTheHeader()
    {
        var (bot, _) = CreateBot(
            HttpStatusCode.TooManyRequests,
            payload: "no-json",
            headers: [new KeyValuePair<string, string>("Retry-After", "3")]);

        var exception = await Assert.ThrowsAsync<TelegramRateLimitException>(() => bot.SendMessageAsync(9, "hola"));

        Assert.Equal(TimeSpan.FromSeconds(3), exception.RetryAfter);
    }
}
