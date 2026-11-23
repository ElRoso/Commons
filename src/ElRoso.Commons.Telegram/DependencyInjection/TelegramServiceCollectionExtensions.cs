// <copyright file="TelegramServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using ElRoso.Services;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using Polly;
    using Polly.CircuitBreaker;
    using Polly.Retry;

    public static class TelegramServiceCollectionExtensions
    {
        /// <summary>
        /// Registra un bot de Telegram con nombre; se llama una vez por bot.
        /// ITelegramBotFactory es Singleton lazy y resuelve todos los bots registrados.
        /// </summary>
        /// <example>
        /// services.AddTelegramBot("alertas", o => o.Token = config["Telegram:BotToken"]!);
        /// services.AddTelegramBot("soporte", o => o.Token = config["Telegram:SupportBotToken"]!);.
        /// </example>
        /// <returns>La misma colección de servicios.</returns>
        public static IServiceCollection AddTelegramBot(
            this IServiceCollection services,
            string botName,
            Action<ElRoso.Telegram.TelegramBotOptions> configure,
            Action<ElRoso.Telegram.TelegramResilienceOptions>? configureResilience = null)
        {
            // Registra los HttpClient una sola vez, con un marker.
            if (!services.Any(s => s.ServiceType == typeof(TelegramHttpClientMarker)))
            {
                services.AddSingleton<TelegramHttpClientMarker>();

                var resilience = new ElRoso.Telegram.TelegramResilienceOptions();
                configureResilience?.Invoke(resilience);

                AddTelegramSendClient(services, resilience);
                AddTelegramPollingClient(services);
            }

            var options = new ElRoso.Telegram.TelegramBotOptions();
            configure(options);

            if (string.IsNullOrWhiteSpace(options.Token))
            {
                throw new InvalidOperationException($"Telegram bot '{botName}': Token cannot be empty.");
            }

            services.AddSingleton(new Services.TelegramBotRegistration { Name = botName, Token = options.Token });

            services.TryAddSingleton<ITelegramBotFactory>(sp =>
            {
                var registrations = sp.GetServices<Services.TelegramBotRegistration>();
                var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                                .CreateLogger<Services.TelegramBot>();

                var bots = registrations.ToDictionary(
                    r => r.Name,
                    r => (ITelegramBot)new Services.TelegramBot(r.Name, r.Token, httpFactory, logger));

                return new Services.TelegramBotFactory(bots);
            });

            return services;
        }

        /// <summary>
        /// Cliente de envío: reintenta 429 y 5xx con backoff y corta con circuit breaker si Telegram está caído.
        /// </summary>
        private static void AddTelegramSendClient(IServiceCollection services, ElRoso.Telegram.TelegramResilienceOptions resilience)
        {
            services
                .AddHttpClient(Services.TelegramClientNames.Send, client => client.Timeout = TimeSpan.FromSeconds(60))

                // Quita el logging por defecto de HttpClient: el token viaja en la URI.
                .RemoveAllLoggers()

                .AddResilienceHandler("telegram-send", pipeline =>
                {
                    // Con cero intentos no se arma la estrategia de reintento: Polly la rechaza.
                    if (resilience.MaxRetryAttempts > 0)
                    {
                        pipeline.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                        {
                            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                                .HandleResult(IsWorthRetrying)
                                .Handle<HttpRequestException>(),
                            MaxRetryAttempts = resilience.MaxRetryAttempts,
                            Delay = resilience.BaseDelay,
                            BackoffType = DelayBackoffType.Exponential,
                            UseJitter = true,

                            // El tiempo que indica Telegram pisa el backoff propio.
                            DelayGenerator = static args => new ValueTask<TimeSpan?>(
                                args.Outcome.Result?.Headers.RetryAfter?.Delta),
                        });
                    }

                    pipeline
                        .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
                        {
                            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                                .HandleResult(IsWorthRetrying)
                                .Handle<HttpRequestException>(),
                            FailureRatio = resilience.CircuitBreakerFailureRatio,
                            BreakDuration = resilience.CircuitBreakerDuration,
                        })
                        .AddTimeout(resilience.AttemptTimeout);
                });
        }

        // Se reintenta solo 429 y 5xx; un timeout no, porque sendMessage no es idempotente.
        private static bool IsWorthRetrying(HttpResponseMessage response) =>
            response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
            || (int)response.StatusCode >= 500;

        /// <summary>
        /// Cliente de polling: sin reintentos y con timeout mayor al long poll de 25 segundos.
        /// </summary>
        private static void AddTelegramPollingClient(IServiceCollection services)
        {
            services
                .AddHttpClient(Services.TelegramClientNames.Polling, client => client.Timeout = TimeSpan.FromSeconds(60))

                // Quita el logging por defecto de HttpClient: el token viaja en la URI.
                .RemoveAllLoggers();
        }

        /// <summary>
        /// Registra el servicio de long-polling para los bots con un ITelegramUpdateHandler; usar cuando no se usan webhooks.
        /// Con update.IsFirstContact en true el handler recibe el chatId a guardar para mensajes salientes.
        /// </summary>
        /// <returns>La misma colección de servicios.</returns>
        public static IServiceCollection AddTelegramPolling(this IServiceCollection services)
        {
            services.AddHostedService<Services.TelegramPollingService>();
            return services;
        }

        /// <summary>
        /// Registra <see cref="ITelegramNotifier"/> con token y destinatarios de la configuración; si faltan registra una implementación no-op.
        /// ENV: <c>Telegram__Notifier__Token</c> y <c>Telegram__Notifier__RecipientIds</c> (chat IDs separados por punto y coma).
        /// </summary>
        /// <example>
        /// appsettings.json: "Telegram": { "Notifier": { "Token": "xxx", "RecipientIds": "123456789;987654321" } }.
        /// </example>
        /// <returns>La misma colección de servicios.</returns>
        public static IServiceCollection AddTelegramNotifier(this IServiceCollection services, IConfiguration configuration)
        {
            var options = configuration.GetSection("Telegram:Notifier").Get<ElRoso.Telegram.TelegramNotifierOptions>()
                ?? new ElRoso.Telegram.TelegramNotifierOptions();

            var recipientIds = options.ParsedRecipientIds;

            if (string.IsNullOrWhiteSpace(options.Token) || recipientIds.Length == 0)
            {
                // Sin configuración registra un no-op.
                services.AddSingleton<ITelegramNotifier, NullTelegramNotifier>();
                return services;
            }

            services.AddTelegramBot("notifier", o => o.Token = options.Token);

            services.AddSingleton<ITelegramNotifier>(sp =>
            {
                var factory = sp.GetRequiredService<ITelegramBotFactory>();
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<Services.TelegramNotifier>();
                var bot = factory.Get("notifier");
                return new Services.TelegramNotifier(bot, recipientIds, logger);
            });

            return services;
        }
    }
}
