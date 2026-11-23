// <copyright file="TelegramMessage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Telegram
{
    using System.Text.Json.Serialization;

    public sealed class TelegramMessage
    {
        [JsonPropertyName("message_id")]
        public long MessageId { get; set; }

        [JsonPropertyName("from")]
        public TelegramUser? From { get; set; }

        [JsonPropertyName("chat")]
        public TelegramChat Chat { get; set; } = null!;

        /// <summary>Gets or sets unix timestamp from Telegram API.</summary>
        [JsonPropertyName("date")]
        public long DateUnix { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonIgnore]
        public DateTime Date => DateTimeOffset.FromUnixTimeSeconds(this.DateUnix).UtcDateTime;

        /// <summary>
        /// Gets a value indicating whether true when the user sends /start (the standard Telegram first-contact command).
        /// The chat.Id returned here is the chatId the system should store to authorize future outbound messages.
        /// Verdadero cuando el usuario envía /start — el chatId debe almacenarse para mensajes salientes futuros.
        /// </summary>
        [JsonIgnore]
        public bool IsFirstContact => this.Text?.TrimStart().StartsWith("/start", StringComparison.OrdinalIgnoreCase) == true;
    }
}
