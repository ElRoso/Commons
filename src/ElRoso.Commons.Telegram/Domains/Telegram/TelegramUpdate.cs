// <copyright file="TelegramUpdate.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Telegram
{
    using System.Text.Json.Serialization;

    public sealed class TelegramUpdate
    {
        [JsonPropertyName("update_id")]
        public long UpdateId { get; set; }

        [JsonPropertyName("message")]
        public TelegramMessage? Message { get; set; }

        /// <summary>Gets shortcut — null when the update carries no message (e.g. edited_message, callback_query).</summary>
        [JsonIgnore]
        public long? ChatId => this.Message?.Chat.Id;

        /// <summary>Gets a value indicating whether true when the user sent /start to initiate contact. Store ChatId.Value to authorize future outbound messages.</summary>
        [JsonIgnore]
        public bool IsFirstContact => this.Message?.IsFirstContact == true;
    }
}
