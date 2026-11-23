namespace ElRoso.Commons.Tests;

using ElRoso.Domains.Configs;
using ElRoso.Services;
using ElRoso.Services.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;

public class CronJobServiceTests
{
    private const string EveryHour = "0 * * * *";

    // EN: Minimal concrete subclass so we can exercise CatchExceptionAsync directly.
    // ES: Subclase concreta mínima para ejercitar CatchExceptionAsync directamente.
    private sealed class TestableJob : CronJobService
    {
        public TestableJob()
            : base(NullLogger<CronJobService>.Instance, EveryHour, TimeZoneInfo.Utc)
        {
        }

        public TestableJob(ITelegramNotifier? notifier)
            : base(NullLogger<CronJobService>.Instance, EveryHour, TimeZoneInfo.Utc, notifier)
        {
        }

        // EN: ScheduleJob is protected; expose it so the reschedule path can be tested.
        // ES: ScheduleJob es protected; se expone para poder testear el camino de reprogramación.
        public Task ScheduleJobForTestAsync(CancellationToken ct = default) => this.ScheduleJob(ct);

        // EN: The timer field is private. Reading it is the only way to assert that a reschedule
        //     releases the previous instance instead of orphaning it.
        // ES: El campo timer es privado. Leerlo es la única forma de verificar que reprogramar
        //     libera la instancia anterior en vez de dejarla huérfana.
        public System.Timers.Timer? GetCurrentTimer() =>
            (System.Timers.Timer?)typeof(CronJobService)
                .GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(this);
    }

    [Fact]
    public async Task CatchExceptionAsync_WhenWorkSucceeds_ReturnsItsResult()
    {
        var sut = new TestableJob();

        var result = await sut.CatchExceptionAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task CatchExceptionAsync_WhenWorkThrows_SwallowsExceptionAndReturnsDefault()
    {
        var sut = new TestableJob();

        var result = await sut.CatchExceptionAsync<int>(() => throw new InvalidOperationException("boom"));

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CatchExceptionAsync_WhenNotifierConfigured_AlertsOnException()
    {
        var notifier = new Mock<ITelegramNotifier>();
        var sut = new TestableJob(notifier.Object);

        await sut.CatchExceptionAsync<int>(() => throw new InvalidOperationException("boom"));

        notifier.Verify(
            n => n.NotifyAsync(It.Is<string>(s => s.Contains("TestableJob") && s.Contains("boom")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CatchExceptionAsync_WhenNotifierIsNull_DoesNotThrow()
    {
        var sut = new TestableJob(notifier: null);

        // EN: Should not throw — null notifier is the legacy/default code path.
        // ES: No debe tirar — notifier null es el path legacy/default.
        var result = await sut.CatchExceptionAsync<int>(() => throw new InvalidOperationException("boom"));

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CatchExceptionAsync_WhenNotifierThrows_StillSwallowsOriginalException()
    {
        // EN: Telegram failure must not propagate — original exception is already logged.
        var notifier = new Mock<ITelegramNotifier>();
        notifier
            .Setup(n => n.NotifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("telegram is down"));

        var sut = new TestableJob(notifier.Object);

        var result = await sut.CatchExceptionAsync<int>(() => throw new InvalidOperationException("boom"));

        Assert.Equal(0, result);
        notifier.Verify(n => n.NotifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CatchExceptionAsync_WhenSuccess_DoesNotCallNotifier()
    {
        var notifier = new Mock<ITelegramNotifier>(MockBehavior.Strict);
        var sut = new TestableJob(notifier.Object);

        var result = await sut.CatchExceptionAsync(() => Task.FromResult("ok"));

        Assert.Equal("ok", result);
        notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ScheduleJob_WhenRescheduled_DisposesThePreviousTimer()
    {
        // EN: A job reschedules itself after every run. Creating a new Timer without releasing the
        //     previous one leaks one Timer — and one Elapsed handler capturing 'this' — per
        //     execution, for the whole life of the process. A job running every minute leaks 1,440
        //     per day.
        // ES: Un job se reprograma después de cada corrida. Crear un Timer nuevo sin liberar el
        //     anterior fuga un Timer — y un handler Elapsed que captura 'this' — por ejecución,
        //     durante toda la vida del proceso. Un job cada minuto fuga 1.440 por día.
        using var sut = new TestableJob();

        await sut.ScheduleJobForTestAsync();
        var firstTimer = sut.GetCurrentTimer();
        Assert.NotNull(firstTimer);

        await sut.ScheduleJobForTestAsync();
        var secondTimer = sut.GetCurrentTimer();

        Assert.NotNull(secondTimer);
        Assert.NotSame(firstTimer, secondTimer);

        // EN: A disposed Timer throws when touched — that is the observable proof it was released.
        // ES: Un Timer dispuesto tira al tocarlo — ésa es la prueba observable de que se liberó.
        Assert.Throws<ObjectDisposedException>(() => firstTimer!.Start());
    }

    [Fact]
    public async Task Dispose_AfterScheduling_ReleasesTheActiveTimer()
    {
        var sut = new TestableJob();
        await sut.ScheduleJobForTestAsync();
        var timer = sut.GetCurrentTimer();

        sut.Dispose();

        Assert.NotNull(timer);
        Assert.Throws<ObjectDisposedException>(() => timer!.Start());
    }

    // EN: A job whose work outlasts its interval used to start a second run on top of the first —
    //     two executions competing over the same rows, with nothing in the log to show for it.
    // ES: Un job cuyo trabajo dura más que su intervalo arrancaba una segunda corrida encima de la
    //     primera — dos ejecuciones compitiendo por las mismas filas, sin nada en el log que lo diga.
    private sealed class BlockingJob : CronJobService
    {
        private readonly TaskCompletionSource release = new();

        public BlockingJob()
            : base(NullLogger<CronJobService>.Instance, EveryHour, TimeZoneInfo.Utc)
        {
        }

        public int Started;

        public int Completed;

        public Task RunOnceForTestAsync(CancellationToken ct = default) => this.RunWithoutOverlapAsync(ct);

        public void Release() => this.release.TrySetResult();

        public override async Task DoWork(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.Started);
            await this.release.Task;
            Interlocked.Increment(ref this.Completed);
        }
    }

    [Fact]
    public async Task RunWithoutOverlap_WhenAPreviousRunIsStillWorking_SkipsTheNewOne()
    {
        using var sut = new BlockingJob();

        var first = sut.RunOnceForTestAsync();

        // EN: Wait until the first run is genuinely inside DoWork before attempting the second.
        while (Volatile.Read(ref sut.Started) == 0)
        {
            await Task.Delay(5);
        }

        await sut.RunOnceForTestAsync();   // must return immediately, without entering DoWork

        Assert.Equal(1, Volatile.Read(ref sut.Started));

        sut.Release();
        await first;

        Assert.Equal(1, Volatile.Read(ref sut.Completed));
    }

    [Fact]
    public async Task RunWithoutOverlap_OnceThePreviousRunFinished_RunsAgain()
    {
        using var sut = new BlockingJob();
        sut.Release();

        await sut.RunOnceForTestAsync();
        await sut.RunOnceForTestAsync();

        Assert.Equal(2, Volatile.Read(ref sut.Started));
    }

    // EN: AddCronJob registers IScheduleConfig<T>, but the job took its cron expression from the
    //     constructor and never read that config — so the expression passed to AddCronJob did
    //     nothing, silently. This overload is what makes the registered configuration reach the job.
    // ES: AddCronJob registra IScheduleConfig<T>, pero el job tomaba su expresión cron del
    //     constructor y nunca leía esa config — así que la expresión pasada a AddCronJob no hacía
    //     nada, en silencio. Esta sobrecarga es la que hace que la configuración registrada llegue.
    private sealed class ConfiguredJob : CronJobService
    {
        public ConfiguredJob(IScheduleConfig<ConfiguredJob> config)
            : base(NullLogger<CronJobService>.Instance, config)
        {
        }

        public Task ScheduleJobForTestAsync(CancellationToken ct = default) => this.ScheduleJob(ct);
    }

    [Fact]
    public async Task CronJobService_CanTakeItsScheduleFromTheRegisteredConfiguration()
    {
        var config = new ScheduleConfig<ConfiguredJob>
        {
            CronExpression = "*/5 * * * *",
            TimeZoneInfo = TimeZoneInfo.Utc,
        };

        using var sut = new ConfiguredJob(config);

        // EN: It schedules without throwing — proof the expression was read and parsed.
        await sut.ScheduleJobForTestAsync();
    }

    [Fact]
    public void AddCronJob_WhenTheCronExpressionIsInvalid_FailsAtStartup()
    {
        // EN: An unparseable expression used to be accepted and only blow up later — or never, when
        //     the job read its schedule from the constructor instead.
        // ES: Una expresión no parseable se aceptaba y explotaba después — o nunca, si el job leía
        //     su schedule del constructor.
        var services = new ServiceCollection();

        Assert.ThrowsAny<ArgumentException>(() =>
            services.AddCronJob<TestableJob>(o => o.CronExpression = "no es una expresion cron"));
    }

    [Fact]
    public void AddCronJob_WhenTheCronExpressionIsValid_Registers()
    {
        var services = new ServiceCollection();

        services.AddCronJob<TestableJob>(o =>
        {
            o.CronExpression = EveryHour;
            o.TimeZoneInfo = TimeZoneInfo.Utc;
        });

        Assert.Contains(services, d => d.ServiceType == typeof(IScheduleConfig<TestableJob>));
    }
}
