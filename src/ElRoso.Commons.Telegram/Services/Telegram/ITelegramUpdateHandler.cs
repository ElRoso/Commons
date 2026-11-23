// <copyright file="ITelegramUpdateHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using ElRoso.Telegram;

    /// <summary>
    /// Implement this interface in the consuming system to process incoming Telegram updates.
    /// When update.IsFirstContact is true, update.ChatId contains the id to store and use for future outbound messages.
    ///
    /// Implementar en el sistema consumidor para procesar updates entrantes de Telegram.
    /// Cuando update.IsFirstContact es true, update.ChatId contiene el id a almacenar para mensajes salientes futuros.
    /// </summary>
    public interface ITelegramUpdateHandler
    {
        /// <summary>Gets must match the botName used in AddTelegramBot(). / Debe coincidir con el botName de AddTelegramBot().</summary>
        string BotName { get; }

        Task HandleAsync(TelegramUpdate update, CancellationToken ct = default);
    }
}
