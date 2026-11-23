// <copyright file="TelegramClientNames.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    /// <summary>
    /// EN: The two named HttpClients Telegram traffic goes through. They are two and not one because
    ///     sending and long-polling want opposite things: a send should be cut short and retried, a
    ///     long poll should be left waiting and never repeated.
    ///     Public because a test — or a consumer that wants to swap the transport — has to be able to
    ///     name them.
    /// ES: Los dos HttpClient nombrados por los que sale el tráfico de Telegram. Son dos y no uno
    ///     porque enviar y hacer long-polling quieren cosas opuestas: un envío conviene cortarlo y
    ///     reintentarlo, un long poll conviene dejarlo esperar y no repetirlo nunca.
    ///     Públicos porque un test — o un consumidor que quiera cambiar el transporte — tiene que
    ///     poder nombrarlos.
    /// </summary>
    public static class TelegramClientNames
    {
        /// <summary>The client that sends messages, with retries and circuit breaker. / El cliente que manda mensajes, con reintentos y circuit breaker.</summary>
        public const string Send = "TelegramBot";

        /// <summary>The client that long-polls for updates, with no retries. / El cliente que hace long-polling de updates, sin reintentos.</summary>
        public const string Polling = "TelegramBotPolling";
    }
}
