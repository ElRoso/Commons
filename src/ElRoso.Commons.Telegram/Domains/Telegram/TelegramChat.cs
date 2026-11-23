// <copyright file="TelegramChat.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Telegram
{
    using System.Text.Json.Serialization;

    public sealed class TelegramChat
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>Gets or sets values: private, group, supergroup, channel.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }
}
