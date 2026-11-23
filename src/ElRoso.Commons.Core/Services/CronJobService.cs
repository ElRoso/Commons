// <copyright file="CronJobService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using Cronos;
    using ElRoso.Domains.Configs;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class CronJobService : IHostedService, IDisposable
    {
        private readonly CronExpression expression;
        private readonly TimeZoneInfo timeZoneInfo;
        private readonly ILogger<CronJobService> logger;
        private readonly ITelegramNotifier? telegramNotifier;

        // EN: One permit: a tick that arrives while the previous run is still working is skipped,
        //     not queued. Two runs of the same job over the same rows is a data problem, and a
        //     queued backlog only postpones the collision.
        // ES: Un solo permiso: un tick que llega mientras la corrida anterior sigue trabajando se
        //     saltea, no se encola. Dos corridas del mismo job sobre las mismas filas es un problema
        //     de datos, y encolarlas solo posterga el choque.
        private readonly SemaphoreSlim executionGate = new(1, 1);

        private System.Timers.Timer? timer;

        protected CronJobService(ILogger<CronJobService> logger, string cronExpression, TimeZoneInfo timeZoneInfo)
            : this(logger, cronExpression, timeZoneInfo, telegramNotifier: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CronJobService"/> class.
        /// EN: Overload that wires up an <see cref="ITelegramNotifier"/> so any exception caught
        ///     by <see cref="CatchExceptionAsync{TResult}"/> also produces a Telegram alert. Pass
        ///     the no-op <c>NullTelegramNotifier</c> (registered by <c>AddTelegramNotifier</c> when
        ///     Telegram credentials are missing) and the call becomes a safe noop.
        /// ES: Sobrecarga que conecta un <see cref="ITelegramNotifier"/> para que toda excepción
        ///     atrapada por <see cref="CatchExceptionAsync{TResult}"/> dispare un alerta de Telegram.
        ///     Pasando el <c>NullTelegramNotifier</c> que registra <c>AddTelegramNotifier</c> cuando
        ///     faltan credenciales, la llamada queda como noop seguro.
        /// </summary>
        protected CronJobService(
            ILogger<CronJobService> logger,
            string cronExpression,
            TimeZoneInfo timeZoneInfo,
            ITelegramNotifier? telegramNotifier)
        {
            this.logger = logger;
            this.expression = CronExpression.Parse(cronExpression);
            this.timeZoneInfo = timeZoneInfo;
            this.telegramNotifier = telegramNotifier;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CronJobService"/> class.
        /// EN: Takes the schedule from the configuration registered by <c>AddCronJob&lt;T&gt;</c>.
        ///     Use this overload — with the constructor that takes a raw cron string, the expression
        ///     passed to <c>AddCronJob</c> is registered in DI and then ignored, which looks like it
        ///     works and does not.
        /// ES: Toma el schedule de la configuración que registra <c>AddCronJob&lt;T&gt;</c>. Usar
        ///     esta sobrecarga — con el constructor que recibe la expresión cron como string, la que
        ///     se le pasa a <c>AddCronJob</c> queda registrada en DI y después se ignora, lo que
        ///     parece que funciona y no funciona.
        /// </summary>
        protected CronJobService(
            ILogger<CronJobService> logger,
            IScheduleConfig config,
            ITelegramNotifier? telegramNotifier = null)
            : this(
                logger,
                config?.CronExpression ?? throw new ArgumentNullException(nameof(config)),
                config.TimeZoneInfo ?? TimeZoneInfo.Utc,
                telegramNotifier)
        {
        }

        public virtual void Dispose()
        {
            this.timer?.Dispose();
            this.executionGate.Dispose();
            GC.SuppressFinalize(this);
        }

        public virtual Task DoWork(CancellationToken cancellationToken)
        {
            return this.CatchExceptionAsync(async () =>
            {
                await Task.Delay(5000, cancellationToken);  // do the work

                return Task.CompletedTask;
            });
        }

        public virtual async Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.LogInformation("[{Job}] Cron Job started.", this.GetType().Name);
            await this.ScheduleJob(cancellationToken);
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            this.timer?.Stop();
            this.logger.LogInformation("[{Job}] Cron Job stopped.", this.GetType().Name);
        }

        [ExcludeFromCodeCoverage]
        public async Task<TResult?> CatchExceptionAsync<TResult>(Func<Task<TResult>> func)
        {
            using (this.logger.BeginScope(this.GetType().Name))
            {
                try
                {
                    this.logger.LogInformation("[{Job}] Task working.", this.GetType().Name);

                    return await func();
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "[{Job}] An error has occurred:", this.GetType().Name);

                    // EN: Best-effort Telegram alert. If the notifier isn't configured (or it throws),
                    //     we don't let it kill the job's exception flow — we already logged the original.
                    // ES: Alerta Telegram best-effort. Si no está configurado (o tira), no rompemos
                    //     el flow de exception del job — la original ya quedó logueada.
                    if (this.telegramNotifier is not null)
                    {
                        try
                        {
                            await this.telegramNotifier.NotifyAsync(
                                $"⚠️ [{this.GetType().Name}] Excepción: {ex.GetType().Name}: {ex.Message}",
                                CancellationToken.None);
                        }
                        catch (Exception alertEx)
                        {
                            this.logger.LogWarning(alertEx, "[{Job}] Failed to send Telegram alert.", this.GetType().Name);
                        }
                    }

                    return default;
                }
                finally
                {
                    this.logger.LogInformation("[{Job}] Task finished.", this.GetType().Name);
                }
            }
        }

        /// <summary>
        /// EN: Runs <see cref="DoWork"/> unless the previous run is still working, in which case
        ///     this tick is skipped and logged at Warning. Without this, a job whose work outlasts
        ///     its interval started a second execution on top of the first — two runs competing over
        ///     the same rows, with nothing in the log to show for it.
        ///     ⚠️ Only the timer path goes through here. A caller invoking <see cref="DoWork"/>
        ///     directly bypasses the gate on purpose: that is a deliberate manual run.
        /// ES: Ejecuta <see cref="DoWork"/> salvo que la corrida anterior siga trabajando, en cuyo
        ///     caso este tick se saltea y se loguea en Warning. Sin esto, un job cuyo trabajo dura
        ///     más que su intervalo arrancaba una segunda ejecución encima de la primera — dos
        ///     corridas compitiendo por las mismas filas, sin nada en el log que lo dijera.
        ///     ⚠️ Solo el camino del timer pasa por acá. Quien llame a <see cref="DoWork"/> directo
        ///     se saltea la compuerta a propósito: eso es una corrida manual deliberada.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected async Task RunWithoutOverlapAsync(CancellationToken cancellationToken)
        {
            if (!await this.executionGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                this.logger.LogWarning(
                    "[{Job}] Previous run is still in progress — skipping this occurrence.",
                    this.GetType().Name);
                return;
            }

            try
            {
                await this.DoWork(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                this.executionGate.Release();
            }
        }

        protected virtual async Task ScheduleJob(CancellationToken cancellationToken)
        {
            // Capture 'now' once to avoid a race condition between GetNextOccurrence and the delay
            // calculation — if DateTimeOffset.Now were called twice, the two values could differ
            // by several milliseconds, causing a slightly negative or incorrect delay.
            // Capturar 'now' una sola vez para evitar race condition entre GetNextOccurrence y el
            // cálculo del delay — si DateTimeOffset.Now se llamara dos veces, los valores podrían
            // diferir por milisegundos, causando un delay negativo o incorrecto.
            var now = DateTimeOffset.Now;
            var next = this.expression.GetNextOccurrence(now, this.timeZoneInfo);
            if (next.HasValue)
            {
                // EN: Release the timer from the previous run before creating the next one. A job
                //     reschedules itself after every execution, so keeping the old instance alive
                //     leaked one Timer — plus its Elapsed handler capturing 'this' — per run, for
                //     the whole life of the process. A job firing every minute leaked 1,440 a day.
                //     Disposing here is safe even when called from inside the old timer's own
                //     Elapsed handler: that handler already stopped it a few lines up.
                // ES: Liberar el timer de la corrida anterior antes de crear el siguiente. Un job se
                //     reprograma después de cada ejecución, así que mantener vivo el anterior fugaba
                //     un Timer — y su handler Elapsed que captura 'this' — por corrida, durante toda
                //     la vida del proceso. Un job cada minuto fugaba 1.440 por día. Disponerlo acá
                //     es seguro incluso llamado desde el propio handler Elapsed del timer viejo: ese
                //     handler ya lo detuvo unas líneas más arriba.
                this.timer?.Dispose();

                var delay = next.Value - now;

                // System.Timers.Timer internally uses an int-based interval, so its maximum safe value
                // is int.MaxValue milliseconds ≈ 24.8 days. We use 24 days as the hard cap to stay safe.
                // System.Timers.Timer usa internamente un intervalo basado en int, por lo que su valor
                // máximo seguro es int.MaxValue milisegundos ≈ 24.8 días. Usamos 24 días como límite.
                var maxSafeDelay = TimeSpan.FromDays(24);

                if (delay <= maxSafeDelay)
                {
                    this.timer = new System.Timers.Timer(delay.TotalMilliseconds);
                    this.timer.Elapsed += async (sender, args) =>
                    {
                        this.timer.Stop();  // reset timer
                        await this.RunWithoutOverlapAsync(cancellationToken);
                        await this.ScheduleJob(cancellationToken);    // reschedule next
                    };
                    this.timer.Start();
                }
                else
                {
                    // The next occurrence is more than 24 days away; re-check every 24 hours.
                    // La próxima ocurrencia es más de 24 días en el futuro; re-verificar cada 24 horas.
                    this.timer = new System.Timers.Timer(TimeSpan.FromDays(1).TotalMilliseconds);
                    this.timer.Elapsed += async (sender, args) =>
                    {
                        this.timer.Stop();  // reset timer
                        await this.ScheduleJob(cancellationToken);    // reschedule next
                    };
                    this.timer.Start();
                }
            }
        }
    }
}
