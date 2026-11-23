// <copyright file="TelegramNotifier.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Sends a message to all configured recipient chat IDs in parallel.
    /// A failure on one recipient is logged and swallowed so the rest still receive the message.
    ///
    /// Envía un mensaje a todos los chat IDs configurados en paralelo.
    /// Un fallo en un destinatario se loguea y no propaga para que los demás sigan recibiendo el mensaje.
    /// </summary>
    internal sealed class TelegramNotifier(
        ITelegramBot bot,
        long[] recipientIds,
        ILogger<TelegramNotifier> logger) : ITelegramNotifier
    {
        public async Task NotifyAsync(string message, CancellationToken ct = default)
        {
            if (recipientIds.Length == 0)
            {
                return;
            }

            var tasks = recipientIds.Select(async chatId =>
            {
                try
                {
                    await bot.SendMessageAsync(chatId, message, ct: ct);
                }
                catch (Exception ex)
                {
                    // One failing recipient must not block others.
                    // Un destinatario fallido no debe bloquear a los demás.
                    logger.LogError(ex, "TelegramNotifier: failed to send to chatId {ChatId}", chatId);
                }
            });

            await Task.WhenAll(tasks);
        }
    }
}
