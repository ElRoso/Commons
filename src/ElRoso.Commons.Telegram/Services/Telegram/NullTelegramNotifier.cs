// <copyright file="NullTelegramNotifier.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    /// <summary>
    /// No-op ITelegramNotifier used when Telegram config is absent or incomplete.
    /// Registered automatically by AddTelegramNotifier when token or recipients are missing.
    ///
    /// ITelegramNotifier vacío usado cuando la config de Telegram está ausente o incompleta.
    /// Lo registra automáticamente AddTelegramNotifier cuando falta el token o los destinatarios.
    /// </summary>
    internal sealed class NullTelegramNotifier : ITelegramNotifier
    {
        public Task NotifyAsync(string message, CancellationToken ct = default) => Task.CompletedTask;
    }
}
