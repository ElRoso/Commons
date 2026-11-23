// <copyright file="TelegramRateLimitException.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Exceptions
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// EN: Telegram refused the message because the bot is sending too fast (HTTP 429). Their API
    ///     limits a bot to roughly 30 messages per second, and 20 per minute to the same group.
    ///     The answer carries how long to wait, and that number is the whole point of this
    ///     exception: retrying without honoring it makes the block longer, and until now the 429
    ///     surfaced as a generic HttpRequestException whose message said nothing about waiting.
    /// ES: Telegram rechazó el mensaje porque el bot está mandando demasiado rápido (HTTP 429). Su
    ///     API limita a un bot a unos 30 mensajes por segundo, y 20 por minuto al mismo grupo.
    ///     La respuesta trae cuánto hay que esperar, y ese número es todo el punto de esta
    ///     excepción: reintentar sin respetarlo alarga el bloqueo, y hasta ahora el 429 aparecía
    ///     como un HttpRequestException genérico cuyo mensaje no decía nada de esperar.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Serializable]
    public class TelegramRateLimitException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TelegramRateLimitException"/> class.
        /// </summary>
        /// <param name="botName">The bot that was throttled. / El bot frenado.</param>
        /// <param name="retryAfter">How long Telegram asked to wait. / Cuánto pidió esperar Telegram.</param>
        public TelegramRateLimitException(string botName, TimeSpan retryAfter)
            : base($"Telegram is rate limiting bot '{botName}'. It asked to wait {retryAfter.TotalSeconds:0.##}s before sending again.")
        {
            this.BotName = botName;
            this.RetryAfter = retryAfter;
        }

        /// <summary>Initializes a new instance of the <see cref="TelegramRateLimitException"/> class.</summary>
        public TelegramRateLimitException()
            : base("Telegram is rate limiting this bot.")
        {
            this.BotName = string.Empty;
        }

        /// <summary>Initializes a new instance of the <see cref="TelegramRateLimitException"/> class.</summary>
        /// <param name="message">The message. / El mensaje.</param>
        public TelegramRateLimitException(string? message)
            : base(message)
        {
            this.BotName = string.Empty;
        }

        /// <summary>Initializes a new instance of the <see cref="TelegramRateLimitException"/> class.</summary>
        /// <param name="message">The message. / El mensaje.</param>
        /// <param name="innerException">The underlying failure. / La falla subyacente.</param>
        public TelegramRateLimitException(string? message, Exception innerException)
            : base(message, innerException)
        {
            this.BotName = string.Empty;
        }

        /// <summary>Gets the bot that was throttled. / El bot que fue frenado.</summary>
        public string BotName { get; }

        /// <summary>
        /// Gets how long Telegram asked to wait before sending again.
        /// ES: Cuánto pidió esperar Telegram antes de volver a mandar.
        /// </summary>
        public TimeSpan RetryAfter { get; }
    }
}
