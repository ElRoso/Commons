// <copyright file="TelegramResilienceOptions.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Telegram
{
    /// <summary>
    /// EN: How hard the sending client insists when Telegram refuses. Shared by every registered bot,
    ///     because they all go out through the same named client.
    ///     🔴 Only refusals are retried — a 429 or a 5xx, where Telegram itself said the message was
    ///     not delivered. A request that timed out is NOT retried: <c>sendMessage</c> is not
    ///     idempotent, and there is no way to tell "it never arrived" from "it arrived and the answer
    ///     was lost". Retrying that sends the message twice, and a person seeing the same alert twice
    ///     trusts the system less than one who sees it late.
    /// ES: Cuánto insiste el cliente de envío cuando Telegram rechaza. Es compartido por todos los
    ///     bots registrados, porque todos salen por el mismo cliente nombrado.
    ///     🔴 Se reintenta solo el rechazo — un 429 o un 5xx, donde el propio Telegram dijo que el
    ///     mensaje no se entregó. Un request que expiró NO se reintenta: <c>sendMessage</c> no es
    ///     idempotente, y no hay forma de distinguir "no llegó nunca" de "llegó y se perdió la
    ///     respuesta". Reintentar eso manda el mensaje dos veces, y una persona que ve la misma
    ///     alerta duplicada le cree menos al sistema que una que la ve tarde.
    /// </summary>
    public sealed class TelegramResilienceOptions
    {
        /// <summary>
        /// Gets or sets how many times a refused send is retried. Zero disables retrying.
        /// ES: Cuántas veces se reintenta un envío rechazado. Cero apaga el reintento.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the first backoff, doubled on each attempt.
        /// ES: La primera espera, que se duplica en cada intento.
        /// </summary>
        /// <remarks>
        /// EN: When Telegram answers a 429 with how long to wait, that number wins over this one —
        ///     insisting sooner than it asked makes the block longer.
        /// ES: Cuando Telegram responde un 429 con cuánto esperar, ese número le gana a éste —
        ///     insistir antes de lo que pidió alarga el bloqueo.
        /// </remarks>
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets how many consecutive refusals open the circuit, as a ratio of the sampled
        /// requests. ES: Qué proporción de rechazos abre el circuito, sobre los requests muestreados.
        /// </summary>
        public double CircuitBreakerFailureRatio { get; set; } = 0.9;

        /// <summary>
        /// Gets or sets how long the circuit stays open — the window where sends fail immediately
        /// instead of waiting for a Telegram that is down.
        /// ES: Cuánto queda abierto el circuito — la ventana donde los envíos fallan al instante en
        /// vez de esperar a un Telegram que está caído.
        /// </summary>
        public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the ceiling for one attempt, including the connection.
        /// ES: El techo de un intento, conexión incluida.
        /// </summary>
        public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(15);
    }
}
