namespace ElRoso.Commons.Tests;

using ElRoso.DependencyInjection;
using ElRoso.Services;
using ElRoso.Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

public class TelegramBotTests
{
    private const string BotName = "alertas";

    private static (ITelegramBotFactory Factory, Mock<HttpMessageHandler> Handler) CreateFactory(
        HttpStatusCode status = HttpStatusCode.OK,
        string payload = """{"ok":true,"result":{"id":1,"is_bot":true,"username":"elroso_bot"}}""")
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(status)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            });

        var services = new ServiceCollection();
        services.AddLogging();

        // EN: No retries: this class checks endpoints and payloads, and a failure test that insists
        //     three times only makes the suite slower. Insisting is TelegramResilienceTests.
        // ES: Sin reintentos: esta clase mira endpoints y payloads, y un test de falla que insiste
        //     tres veces solo hace más lenta la suite. Insistir es TelegramResilienceTests.
        services.AddTelegramBot(BotName, o => o.Token = "123:fake-token", r => r.MaxRetryAttempts = 0);

        // EN: Both named clients — sending and polling go through different ones since F-07, and
        //     a test that stubs only one lets the other reach Telegram for real.
        // ES: Los dos clientes nombrados — envío y polling salen por clientes distintos desde F-07, y
        //     un test que reemplaza uno solo deja al otro llegando a Telegram de verdad.
        services.AddHttpClient(TelegramClientNames.Send).ConfigurePrimaryHttpMessageHandler(() => handler.Object);
        services.AddHttpClient(TelegramClientNames.Polling).ConfigurePrimaryHttpMessageHandler(() => handler.Object);

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<ITelegramBotFactory>(), handler);
    }

    [Fact]
    public void AddTelegramBot_WithAnEmptyToken_FailsAtStartup()
    {
        // EN: The one Telegram setting whose absence is fatal — a bot with no token is a bot that
        //     will fail on its first message, hours later.
        // ES: El único setting de Telegram cuya ausencia es fatal — un bot sin token es un bot que
        //     va a fallar en su primer mensaje, horas después.
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddTelegramBot("x", o => o.Token = " "));
    }

    [Fact]
    public void Factory_ForAnUnregisteredBot_SaysWhichOneAndHowToFixIt()
    {
        var (factory, _) = CreateFactory();

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Get("no-registrado"));

        Assert.Contains("no-registrado", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddTelegramBot", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Factory_ResolvesTheRegisteredBotByName()
    {
        var (factory, _) = CreateFactory();

        Assert.Equal(BotName, factory.Get(BotName).Name);
    }

    [Fact]
    public async Task GetMeAsync_ReturnsTheBotIdentity()
    {
        var (factory, _) = CreateFactory();

        var info = await factory.Get(BotName).GetMeAsync();

        Assert.Equal("elroso_bot", info.Username);
    }

    [Fact]
    public async Task GetMeAsync_WhenTelegramAnswersNotOk_Throws()
    {
        var (factory, _) = CreateFactory(payload: """{"ok":false,"description":"Unauthorized"}""");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.Get(BotName).GetMeAsync());

        Assert.Contains("Unauthorized", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendMessageAsync_PostsToTheSendMessageEndpoint()
    {
        var (factory, handler) = CreateFactory(payload: """{"ok":true}""");

        await factory.Get(BotName).SendMessageAsync(chatId: 42, "hola");

        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("/sendMessage", StringComparison.Ordinal)),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_WhenTelegramFails_Throws()
    {
        // EN: A 429 used to be the failure exercised here, and it now has its own type and its own
        //     tests — see TelegramFormattingTests. Anything else still surfaces as HttpRequestException.
        // ES: Acá la falla ejercitada era un 429, que ahora tiene su propio tipo y sus propios tests
        //     — ver TelegramFormattingTests. El resto sigue apareciendo como HttpRequestException.
        var (factory, _) = CreateFactory(status: HttpStatusCode.InternalServerError, payload: """{"ok":false}""");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => factory.Get(BotName).SendMessageAsync(chatId: 42, "hola"));
    }

    [Fact]
    public async Task SendDocumentAsync_PostsToTheSendDocumentEndpoint()
    {
        var (factory, handler) = CreateFactory(payload: """{"ok":true}""");

        await factory.Get(BotName).SendDocumentAsync(42, "https://cdn/factura.pdf", "Factura");

        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.AbsolutePath.EndsWith("/sendDocument", StringComparison.Ordinal)),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendPhotoAsync_PostsToTheSendPhotoEndpoint()
    {
        var (factory, handler) = CreateFactory(payload: """{"ok":true}""");

        await factory.Get(BotName).SendPhotoAsync(42, "https://cdn/foto.png");

        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.AbsolutePath.EndsWith("/sendPhoto", StringComparison.Ordinal)),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetUpdatesAsync_WithNoUpdates_ReturnsAnEmptyList()
    {
        var (factory, _) = CreateFactory(payload: """{"ok":true,"result":[]}""");

        var updates = await factory.Get(BotName).GetUpdatesAsync();

        Assert.Empty(updates);
    }

    [Fact]
    public async Task GetUpdatesAsync_SendsTheOffsetSoTelegramDoesNotResend()
    {
        var (factory, handler) = CreateFactory(payload: """{"ok":true,"result":[]}""");

        await factory.Get(BotName).GetUpdatesAsync(offset: 77, limit: 10, timeoutSeconds: 25);

        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.Query.Contains("offset=77", StringComparison.Ordinal)),
            ItExpr.IsAny<CancellationToken>());
    }

    // ── Modelo de updates ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/start", true)]
    [InlineData("  /start", true)]
    [InlineData("/START", true)]
    [InlineData("hola", false)]
    [InlineData(null, false)]
    public void TelegramUpdate_DetectsTheFirstContact(string? text, bool expected)
    {
        // EN: /start is the moment the chat id must be stored — miss it and the system can never
        //     write to that user again.
        // ES: /start es el momento en que hay que guardar el chat id — si se pierde, el sistema no
        //     puede volver a escribirle a esa persona nunca más.
        var update = new TelegramUpdate
        {
            UpdateId = 1,
            Message = new TelegramMessage { Text = text, Chat = new TelegramChat { Id = 99 } },
        };

        Assert.Equal(expected, update.IsFirstContact);
    }

    [Fact]
    public void TelegramUpdate_WithoutAMessage_HasNoChatId()
    {
        var update = new TelegramUpdate { UpdateId = 1 };

        Assert.Null(update.ChatId);
        Assert.False(update.IsFirstContact);
    }

    [Fact]
    public void TelegramMessage_ConvertsTheUnixTimestamp()
    {
        var message = new TelegramMessage { DateUnix = 1_700_000_000, Chat = new TelegramChat { Id = 1 } };

        Assert.Equal(new DateTime(2023, 11, 14, 22, 13, 20, DateTimeKind.Utc), message.Date);
    }

    [Fact]
    public void TelegramUpdate_DeserializesTheSnakeCasePayload()
    {
        // EN: Telegram sends snake_case; the mapping is by attribute and a rename would break it
        //     silently — the update would arrive with everything null.
        // ES: Telegram manda snake_case; el mapeo es por atributo y un renombre lo rompería en
        //     silencio — el update llegaría con todo en null.
        const string payload = """
        {"update_id":10,"message":{"message_id":5,"date":1700000000,"text":"/start",
         "chat":{"id":123,"type":"private"},"from":{"id":9,"is_bot":false,"first_name":"Ana"}}}
        """;

        var update = JsonSerializer.Deserialize<TelegramUpdate>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(10, update!.UpdateId);
        Assert.Equal(123, update.ChatId);
        Assert.True(update.IsFirstContact);
    }

    // ── Notifier ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTelegramNotifier_WithoutConfiguration_RegistersTheNoOp()
    {
        // EN: A missing integration must not stop the app from starting.
        // ES: Una integración sin configurar no debe impedir que la app arranque.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelegramNotifier(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var notifier = provider.GetRequiredService<ITelegramNotifier>();

        Assert.Equal("NullTelegramNotifier", notifier.GetType().Name);
    }

    [Fact]
    public void AddTelegramNotifier_WithoutRecipients_RegistersTheNoOp()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Telegram:Notifier:Token"] = "123:fake" })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelegramNotifier(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Equal("NullTelegramNotifier", provider.GetRequiredService<ITelegramNotifier>().GetType().Name);
    }

    [Fact]
    public void AddTelegramNotifier_FullyConfigured_RegistersTheRealOne()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telegram:Notifier:Token"] = "123:fake",
                ["Telegram:Notifier:RecipientIds"] = "111;222",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelegramNotifier(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Equal("TelegramNotifier", provider.GetRequiredService<ITelegramNotifier>().GetType().Name);
    }

    [Theory]
    [InlineData("111;222", 2)]
    [InlineData(" 111 ; 222 ; ", 2)]
    [InlineData("111;no-es-un-numero;222", 2)]
    [InlineData("", 0)]
    public void TelegramNotifierOptions_ParsesTheRecipientList(string raw, int expected)
    {
        var options = new TelegramNotifierOptions { RecipientIds = raw };

        Assert.Equal(expected, options.GetType()
            .GetProperty("ParsedRecipientIds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(options) is long[] ids ? ids.Length : -1);
    }

    [Fact]
    public async Task AddTelegramPolling_WithoutHandlers_StopsInsteadOfSpinning()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelegramBot(BotName, o => o.Token = "123:fake");
        services.AddTelegramPolling();

        using var provider = services.BuildServiceProvider();
        var hosted = provider.GetRequiredService<IEnumerable<Microsoft.Extensions.Hosting.IHostedService>>();
        var polling = Assert.Single(hosted);

        await polling.StartAsync(CancellationToken.None);
        await polling.StopAsync(CancellationToken.None);
    }

    // ── El token no debe filtrarse ───────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenTelegramFails_TheExceptionDoesNotCarryTheToken()
    {
        // EN: The Telegram API puts the token in the URL — that is how their API works, there is no
        //     header alternative. The risk is everything that copies a URL around: HttpClient
        //     logging, an EnsureSuccessStatusCode message, an exception that reaches a log
        //     aggregator. A bot token is a full credential: whoever has it can read and write every
        //     conversation of that bot.
        // ES: La API de Telegram pone el token en la URL — así funciona su API, no hay alternativa
        //     por header. El riesgo es todo lo que copia una URL a otro lado: el logging de
        //     HttpClient, el mensaje de EnsureSuccessStatusCode, una excepción que llega a un
        //     agregador de logs. Un token de bot es una credencial completa: quien lo tiene puede
        //     leer y escribir toda conversación de ese bot.
        var (factory, _) = CreateFactory(status: HttpStatusCode.BadRequest, payload: """{"ok":false}""");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => factory.Get(BotName).SendMessageAsync(chatId: 42, "hola"));

        Assert.DoesNotContain("fake-token", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenGetMeFails_TheExceptionDoesNotCarryTheToken()
    {
        var (factory, _) = CreateFactory(status: HttpStatusCode.Unauthorized, payload: """{"ok":false}""");

        var exception = await Record.ExceptionAsync(() => factory.Get(BotName).GetMeAsync());

        Assert.NotNull(exception);
        Assert.DoesNotContain("fake-token", exception.ToString(), StringComparison.Ordinal);
    }

    // EN: Captures everything written to the logging pipeline, to check what actually travels.
    // ES: Captura todo lo que se escribe al pipeline de logging, para ver qué viaja de verdad.
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

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (lines)
                {
                    lines.Add(formatter(state, exception));
                }
            }
        }
    }

    [Fact]
    public async Task HttpClientLogging_DoesNotWriteTheTokenToTheLog()
    {
        // EN: 🔴 The real exposure. HttpClient's own logging writes the full request URI at
        //     Information level, and the Telegram API carries the token IN the URI. Turn on
        //     HttpClient logging — which any app can do without thinking about Telegram — and every
        //     bot token lands in the log, and from there in Seq, Graylog or a log file that gets
        //     shared around. A bot token is a full credential.
        // ES: 🔴 La exposición real. El logging propio de HttpClient escribe la URI completa del
        //     request en nivel Information, y la API de Telegram lleva el token EN la URI. Con el
        //     logging de HttpClient encendido — algo que cualquier app puede hacer sin pensar en
        //     Telegram — todo token de bot termina en el log, y de ahí en Seq, Graylog o un archivo
        //     que se comparte. Un token de bot es una credencial completa.
        var capture = new CapturingLoggerProvider();

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true}""", System.Text.Encoding.UTF8, "application/json"),
            });

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture).SetMinimumLevel(LogLevel.Trace));
        services.AddTelegramBot(BotName, o => o.Token = "123:fake-token");
        services.AddHttpClient(TelegramClientNames.Send).ConfigurePrimaryHttpMessageHandler(() => handler.Object);
        services.AddHttpClient(TelegramClientNames.Polling).ConfigurePrimaryHttpMessageHandler(() => handler.Object);

        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<ITelegramBotFactory>().Get(BotName).SendMessageAsync(42, "hola");

        var everythingLogged = string.Join("\n", capture.Lines);
        Assert.DoesNotContain("fake-token", everythingLogged, StringComparison.Ordinal);
    }
}
