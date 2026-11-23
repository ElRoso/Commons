// <copyright file="TelegramNotifierOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Telegram
{
    /// <summary>
    /// Configuration for ITelegramNotifier — bot token and broadcast recipients.
    /// Bind from appsettings section "Telegram:Notifier" or ENV vars Telegram__Notifier__Token / Telegram__Notifier__RecipientIds.
    ///
    /// Configuración para ITelegramNotifier — token del bot y destinatarios del broadcast.
    /// Bindear desde sección "Telegram:Notifier" o ENV Telegram__Notifier__Token / Telegram__Notifier__RecipientIds.
    /// </summary>
    public sealed class TelegramNotifierOptions
    {
        public string Token { get; set; } = string.Empty;

        /// <summary>Gets or sets semicolon-separated chat IDs. E.g. "123456789;987654321". / Chat IDs separados por punto y coma.</summary>
        public string RecipientIds { get; set; } = string.Empty;

        /// <summary>Gets parses RecipientIds string into a long array. Invalid entries are silently skipped.</summary>
        internal long[] ParsedRecipientIds =>
            this.RecipientIds
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(id => long.TryParse(id, out var parsed) ? (long?)parsed : null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToArray();
    }
}
