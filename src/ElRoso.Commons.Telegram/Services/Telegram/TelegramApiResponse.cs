// <copyright file="TelegramApiResponse.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using System.Text.Json.Serialization;

    internal sealed class TelegramApiResponse<T>
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public T? Result { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the extra data Telegram attaches to an error. Today only the wait of a 429
        /// is read from here — and it is the authoritative source for it, more than the Retry-After
        /// header.
        /// ES: Datos extra que Telegram adjunta a un error. Hoy solo se lee de acá la espera de un
        /// 429 — y es la fuente que manda para ese dato, más que el header Retry-After.
        /// </summary>
        [JsonPropertyName("parameters")]
        public TelegramResponseParameters? Parameters { get; set; }
    }
}
