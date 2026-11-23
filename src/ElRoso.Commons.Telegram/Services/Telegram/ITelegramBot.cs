// <copyright file="ITelegramBot.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using ElRoso.Telegram;

    public interface ITelegramBot
    {
        string Name { get; }

        /// <summary>Validates the bot token and returns bot metadata. / Valida el token y retorna metadata del bot.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<TelegramBotInfo> GetMeAsync(CancellationToken ct = default);

        /// <summary>
        /// EN: Sends a text message. Without <paramref name="options"/> it goes out as plain text,
        ///     exactly as it did before formatting existed.
        /// ES: Manda un mensaje de texto. Sin <paramref name="options"/> sale como texto plano,
        ///     exactamente igual que antes de que existiera el formato.
        /// </summary>
        /// <param name="chatId">The chat. / El chat.</param>
        /// <param name="message">The text. / El texto.</param>
        /// <param name="options">Formatting, notification and buttons. / Formato, notificación y botones.</param>
        /// <param name="ct">Cancellation token. / Token de cancelación.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task SendMessageAsync(long chatId, string message, TelegramSendOptions? options = null, CancellationToken ct = default);

        Task SendDocumentAsync(long chatId, string documentUrl, string? caption = null, TelegramSendOptions? options = null, CancellationToken ct = default);

        Task SendPhotoAsync(long chatId, string photoUrl, string? caption = null, TelegramSendOptions? options = null, CancellationToken ct = default);

        /// <summary>
        /// Fetches pending updates (short or long polling depending on timeoutSeconds).
        /// Use offset = lastUpdateId + 1 to acknowledge processed updates.
        /// Obtiene updates pendientes. Usa offset = lastUpdateId + 1 para reconocer updates procesados.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long? offset = null, int limit = 100, int timeoutSeconds = 0, CancellationToken ct = default);
    }
}
