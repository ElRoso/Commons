// <copyright file="ITelegramNotifier.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    /// <summary>
    /// Broadcasts a text message to all configured Telegram recipients. Always safe to call — no-op when not configured.
    /// Register with AddTelegramNotifier(configuration) in DI setup.
    ///
    /// Envía un mensaje de texto a todos los destinatarios de Telegram configurados. Siempre seguro de llamar — no-op si no está configurado.
    /// Registrar con AddTelegramNotifier(configuration) en la configuración de DI.
    /// </summary>
    public interface ITelegramNotifier
    {
        Task NotifyAsync(string message, CancellationToken ct = default);
    }
}
