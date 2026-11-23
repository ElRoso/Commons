// <copyright file="TelegramPollingService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Background service that polls Telegram for updates and dispatches them to all registered ITelegramUpdateHandler.
    /// Uses long-polling (25s timeout) to minimize unnecessary HTTP round trips.
    /// Register with AddTelegramPolling() — only use if not using webhooks.
    ///
    /// Servicio en segundo plano que hace long-polling a Telegram y despacha updates a los handlers registrados.
    /// Usar AddTelegramPolling() — solo si no se usan webhooks.
    /// </summary>
    public sealed class TelegramPollingService(
        ITelegramBotFactory botFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramPollingService> logger) : BackgroundService
    {
        // Long-polling: Telegram holds the connection for up to this many seconds if no updates.
        private const int LongPollingTimeoutSeconds = 25;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Discover which bots have handlers registered
            using var discoveryScope = scopeFactory.CreateScope();
            var allHandlers = discoveryScope.ServiceProvider.GetServices<ITelegramUpdateHandler>().ToList();

            var botNames = allHandlers.Select(h => h.BotName).Distinct().ToList();
            if (botNames.Count == 0)
            {
                logger.LogWarning("TelegramPollingService started but no ITelegramUpdateHandler implementations are registered.");
                return;
            }

            logger.LogInformation("Telegram long-polling started for bots: {Bots}", string.Join(", ", botNames));

            // EN: One loop per bot, all running at once. The loop used to walk the bots one after
            //     another, and with a 25-second long poll that means bot N waits for the N-1 before
            //     it: with three bots, a message to the last one could sit unanswered for 75
            //     seconds. Worse, a single slow or hanging bot delayed everyone else's messages.
            //     Each bot now has its own offset and its own failures, and none of them can block
            //     another.
            // ES: Un loop por bot, todos a la vez. El loop recorría los bots uno tras otro, y con un
            //     long poll de 25 segundos eso significa que el bot N espera a los N-1 anteriores:
            //     con tres bots, un mensaje al último podía quedar sin respuesta 75 segundos. Peor:
            //     un solo bot lento o colgado demoraba los mensajes de todos los demás. Ahora cada
            //     bot tiene su propio offset y sus propias fallas, y ninguno puede bloquear a otro.
            await Task.WhenAll(botNames.Select(botName => this.PollBotAsync(botName, stoppingToken)));

            logger.LogInformation("Telegram long-polling stopped.");
        }

        private async Task PollBotAsync(string botName, CancellationToken stoppingToken)
        {
            long? offset = null;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var bot = botFactory.Get(botName);
                    var updates = await bot.GetUpdatesAsync(
                        offset: offset,
                        timeoutSeconds: LongPollingTimeoutSeconds,
                        ct: stoppingToken);

                    if (updates.Count == 0)
                    {
                        continue;
                    }

                    // Create a scope per batch so handlers can consume scoped services (DAOs, etc.)
                    // Crear un scope por batch para que los handlers puedan usar servicios scoped (DAOs, etc.)
                    using var scope = scopeFactory.CreateScope();
                    var handlers = scope.ServiceProvider
                        .GetServices<ITelegramUpdateHandler>()
                        .Where(h => h.BotName == botName)
                        .ToList();

                    foreach (var update in updates)
                    {
                        foreach (var handler in handlers)
                        {
                            try
                            {
                                await handler.HandleAsync(update, stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                // EN: A consumer's handler blowing up must not take this bot's loop
                                //     down, let alone the other bots'.
                                // ES: Que explote el handler de un consumidor no debe llevarse el
                                //     loop de este bot, y menos el de los demás.
                                logger.LogError(
                                    ex,
                                    "Handler {Handler} failed for updateId {UpdateId} on bot '{BotName}'",
                                    handler.GetType().Name,
                                    update.UpdateId,
                                    botName);
                            }
                        }

                        // Advance offset past this update so Telegram doesn't resend it
                        offset = update.UpdateId + 1;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Telegram polling error for bot '{BotName}'. Retrying in 5s.", botName);

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            logger.LogInformation("Telegram long-polling stopped for bot '{BotName}'.", botName);
        }
    }
}
