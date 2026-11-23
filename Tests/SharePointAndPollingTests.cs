namespace ElRoso.Commons.Tests;

using ElRoso.DependencyInjection;
using ElRoso.Services;
using ElRoso.Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using System.Net;

public class SharePointAndPollingTests
{
    // ── SharePoint ───────────────────────────────────────────────────────────────────────────

    private static ISharePointFileService CreateSharePoint(bool configured)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AzureSharePoint:Site"] = "elroso.sharepoint.com",
            ["AzureSharePoint:SubSite"] = "Facturacion",
        };

        if (configured)
        {
            settings["AzureSharePoint:TenantId"] = "00000000-0000-0000-0000-000000000000";
            settings["AzureSharePoint:ClientId"] = "00000000-0000-0000-0000-000000000001";
            settings["AzureSharePoint:ClientSecret"] = "fake-secret";
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new SharePointFileService(configuration, NullLogger<SharePointFileService>.Instance);
    }

    [Fact]
    public void SharePointFileService_WithoutCredentials_StillConstructs()
    {
        // EN: Deliberate posture: a missing optional integration must not stop the app from
        //     starting. It fails when actually used, not at boot.
        // ES: Postura deliberada: una integración opcional sin configurar no debe impedir que la app
        //     arranque. Falla al usarse, no al arrancar.
        var exception = Record.Exception(() => CreateSharePoint(configured: false));

        Assert.Null(exception);
    }

    [Fact]
    public async Task UploadFromFileInfo_WithoutCredentials_SaysWhatIsMissing()
    {
        var sut = CreateSharePoint(configured: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.UploadFileSharePointAsync(new FileInfo("/tmp/x.pdf")));

        Assert.Contains("AzureSharePoint", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadFromBytes_WithoutCredentials_Throws()
    {
        var sut = CreateSharePoint(configured: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.UploadFileSharePointAsync([1, 2, 3], "x.pdf"));
    }

    [Fact]
    public async Task Download_WithoutCredentials_Throws()
    {
        var sut = CreateSharePoint(configured: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DownloadFileSharePointAsync("carpeta/x.pdf"));
    }

    [Fact]
    public async Task Delete_WithoutCredentials_Throws()
    {
        var sut = CreateSharePoint(configured: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DeleteFileSharePointAsync("carpeta/x.pdf"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Download_WithAnEmptyPath_Throws(string path)
    {
        var sut = CreateSharePoint(configured: true);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.DownloadFileSharePointAsync(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Delete_WithAnEmptyPath_Throws(string path)
    {
        var sut = CreateSharePoint(configured: true);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.DeleteFileSharePointAsync(path));
    }

    // ── Long-polling ─────────────────────────────────────────────────────────────────────────

    private sealed class RecordingHandler : ITelegramUpdateHandler
    {
        public string BotName => "alertas";

        public List<long> SeenUpdates { get; } = [];

        public TaskCompletionSource FirstSeen { get; } = new();

        public Task HandleAsync(TelegramUpdate update, CancellationToken ct = default)
        {
            this.SeenUpdates.Add(update.UpdateId);
            this.FirstSeen.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : ITelegramUpdateHandler
    {
        public string BotName => "alertas";

        public TaskCompletionSource Called { get; } = new();

        public Task HandleAsync(TelegramUpdate update, CancellationToken ct = default)
        {
            this.Called.TrySetResult();
            throw new InvalidOperationException("el handler del consumidor exploto");
        }
    }

    private static ServiceProvider BuildPollingProvider(ITelegramUpdateHandler handler, string payload)
    {
        var messageHandler = new Mock<HttpMessageHandler>();
        messageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelegramBot("alertas", o => o.Token = "123:fake");
        services.AddHttpClient(TelegramClientNames.Send).ConfigurePrimaryHttpMessageHandler(() => messageHandler.Object);
        services.AddHttpClient(TelegramClientNames.Polling).ConfigurePrimaryHttpMessageHandler(() => messageHandler.Object);
        services.AddSingleton(handler);
        services.AddTelegramPolling();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Polling_DispatchesUpdatesToTheHandler()
    {
        var handler = new RecordingHandler();
        using var provider = BuildPollingProvider(
            handler,
            """{"ok":true,"result":[{"update_id":7,"message":{"message_id":1,"date":1700000000,"text":"/start","chat":{"id":55,"type":"private"}}}]}""");

        var polling = provider.GetRequiredService<IEnumerable<IHostedService>>().Single();

        await polling.StartAsync(CancellationToken.None);
        await handler.FirstSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await polling.StopAsync(CancellationToken.None);

        Assert.Contains(7, handler.SeenUpdates);
    }

    [Fact]
    public async Task Polling_WhenAHandlerThrows_KeepsRunning()
    {
        // EN: A consumer's handler blowing up must not take the polling loop down with it — the
        //     rest of the bots and the next updates have to keep working.
        // ES: Que explote el handler de un consumidor no debe llevarse puesto el loop de polling —
        //     el resto de los bots y los updates siguientes tienen que seguir funcionando.
        var handler = new ThrowingHandler();
        using var provider = BuildPollingProvider(
            handler,
            """{"ok":true,"result":[{"update_id":8,"message":{"message_id":1,"date":1700000000,"text":"hola","chat":{"id":55,"type":"private"}}}]}""");

        var polling = provider.GetRequiredService<IEnumerable<IHostedService>>().Single();

        await polling.StartAsync(CancellationToken.None);
        await handler.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var exception = await Record.ExceptionAsync(() => polling.StopAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    // ── Servicio base ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ElRosoServiceBase_DisposeIsSafeToCallTwice()
    {
        var service = new ElRosoServiceBase(NullLogger<SharePointAndPollingTests>.Instance);

        service.Dispose();
        var exception = Record.Exception(service.Dispose);

        Assert.Null(exception);
    }

    // ── Varios bots en paralelo ──────────────────────────────────────────────────────────────

    private sealed class NamedHandler(string botName, TaskCompletionSource seen) : ITelegramUpdateHandler
    {
        public string BotName => botName;

        public TaskCompletionSource Seen => seen;

        public Task HandleAsync(TelegramUpdate update, CancellationToken ct = default)
        {
            seen.TrySetResult();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Polling_WithSeveralBots_PollsThemInParallel()
    {
        // EN: The loop used to walk the bots one after another. With a 25-second long poll, bot N
        //     waits for the N-1 before it: with three bots, a message to the last one could sit
        //     unanswered for 75 seconds. Worse, a slow bot delays every other bot's messages.
        //     Here the first bot's request never returns; if polling were serial, the second bot
        //     would never be reached and this test would time out.
        // ES: El loop recorría los bots uno tras otro. Con un long poll de 25 segundos, el bot N
        //     espera a los N-1 anteriores: con tres bots, un mensaje al último podía quedar sin
        //     respuesta 75 segundos. Peor: un bot lento demora los mensajes de todos los demás.
        //     Acá el request del primer bot nunca vuelve; si el polling fuera en serie, al segundo
        //     bot no se llegaría nunca y este test expiraría.
        var nuncaResponde = new TaskCompletionSource();
        var rapidoVisto = new TaskCompletionSource();
        var respuestasAlRapido = 0;

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, ct) =>
            {
                var esElLento = request.RequestUri!.ToString().Contains("bot111:", StringComparison.Ordinal);

                // EN: The slow bot never answers. The fast one answers once and then blocks like a
                //     real long poll does — otherwise its loop would spin at full speed against an
                //     instant mock and keep the test host busy after the test ends.
                // ES: El bot lento no responde nunca. El rápido responde una vez y después bloquea,
                //     como hace un long poll de verdad — si no, su loop giraría a toda velocidad
                //     contra un mock instantáneo y dejaría ocupado al host de pruebas al terminar.
                if (esElLento || Interlocked.Increment(ref respuestasAlRapido) > 1)
                {
                    await nuncaResponde.Task.WaitAsync(ct);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"ok":true,"result":[{"update_id":1,"message":{"message_id":1,"date":1700000000,"text":"hola","chat":{"id":9,"type":"private"}}}]}""",
                        System.Text.Encoding.UTF8,
                        "application/json"),
                };
            });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelegramBot("lento", o => o.Token = "111:token");
        services.AddTelegramBot("rapido", o => o.Token = "222:token");
        services.AddHttpClient(TelegramClientNames.Send).ConfigurePrimaryHttpMessageHandler(() => handler.Object);
        services.AddHttpClient(TelegramClientNames.Polling).ConfigurePrimaryHttpMessageHandler(() => handler.Object);
        services.AddSingleton<ITelegramUpdateHandler>(new NamedHandler("lento", new TaskCompletionSource()));
        services.AddSingleton<ITelegramUpdateHandler>(new NamedHandler("rapido", rapidoVisto));
        services.AddTelegramPolling();

        using var provider = services.BuildServiceProvider();
        var polling = provider.GetRequiredService<IEnumerable<IHostedService>>().Single();

        await polling.StartAsync(CancellationToken.None);

        // EN: The fast bot must be served while the slow one is still hanging.
        // ES: El bot rápido tiene que ser atendido mientras el lento sigue colgado.
        await rapidoVisto.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // EN: Stopping cancels both loops out of their pending requests; nothing must survive the
        //     test. ES: El stop saca a los dos loops de sus requests pendientes; nada debe
        //     sobrevivir al test.
        await polling.StopAsync(CancellationToken.None);
        nuncaResponde.TrySetCanceled();
    }
}
