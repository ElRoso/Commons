// <copyright file="TelegramResponseParameters.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using System.Text.Json.Serialization;

    internal sealed class TelegramResponseParameters
    {
        /// <summary>Gets or sets the seconds to wait before sending again. / Segundos a esperar antes de volver a mandar.</summary>
        [JsonPropertyName("retry_after")]
        public int? RetryAfter { get; set; }
    }
}
