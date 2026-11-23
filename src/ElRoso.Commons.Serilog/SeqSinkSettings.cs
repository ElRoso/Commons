// <copyright file="SeqSinkSettings.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Commons
{
    using System;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// EN: What the Seq sink gets told, read off configuration once so a test can pin it.
    ///
    ///     🔴 The API key is OPTIONAL and that is the whole point of this type. A Seq with
    ///     authentication enabled refuses anonymous ingestion with
    ///     <c>401 "A valid API key is required for writing events"</c>, and
    ///     <c>Serilog.Sinks.Seq</c> swallows that failure: the app logs normally, the sink is
    ///     registered, the network is fine, and the server stays empty. Nothing in the consumer
    ///     app can tell the difference — which is why the key travels from configuration and why
    ///     leaving it out has to keep working for a Seq that ingests anonymously.
    ///
    ///     ⚠️ A section without <c>ServerURL</c> is NOT a sink. Registering one on an empty address
    ///     drops every event into it and looks identical to a Seq that is up and receiving nothing.
    ///
    /// ES: Qué se le dice al sink de Seq, leído de la configuración una sola vez para que un test
    ///     lo pueda fijar.
    ///
    ///     🔴 La API key es OPCIONAL y ésa es toda la razón de este tipo. Un Seq con autenticación
    ///     prendida rechaza la ingesta anónima con
    ///     <c>401 "A valid API key is required for writing events"</c>, y
    ///     <c>Serilog.Sinks.Seq</c> se traga esa falla: la app loguea normal, el sink está
    ///     registrado, la red anda, y el servidor queda vacío. Nada en la app consumidora nota la
    ///     diferencia — por eso la key viaja desde la configuración, y por eso omitirla tiene que
    ///     seguir funcionando contra un Seq que ingesta anónimo.
    ///
    ///     ⚠️ Una sección sin <c>ServerURL</c> NO es un sink. Registrar uno sobre una dirección
    ///     vacía tira ahí cada evento y se ve idéntico a un Seq levantado que no recibe nada.
    /// </summary>
    internal sealed record SeqSinkSettings
    {
        /// <summary>EN/ES: Configuration section.</summary>
        internal const string Section = "SEQ";

        /// <summary>Gets EN: where Seq listens. ES: dónde escucha Seq.</summary>
        internal required string ServerUrl { get; init; }

        /// <summary>Gets EN: the key, or null when Seq ingests anonymously. ES: la key, o null si Seq ingesta anónimo.</summary>
        internal string? ApiKey { get; init; }

        /// <summary>
        /// EN: The Seq sink's settings, or null when there is no sink to register.
        /// ES: Los ajustes del sink de Seq, o null si no hay sink que registrar.
        /// </summary>
        /// <param name="configuration">EN: App configuration. ES: Configuración.</param>
        /// <returns>EN: Settings or null. ES: Ajustes o null.</returns>
        internal static SeqSinkSettings? Read(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            if (!configuration.GetSection(Section).Exists())
            {
                return null;
            }

            var serverUrl = configuration.GetValue<string>($"{Section}:ServerURL");
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                return null;
            }

            // EN: A blank key is somebody who set the variable and left it empty — that is "no
            //     key", not a key made of spaces, and sending it verbatim would earn a 401 from a
            //     Seq that would otherwise have accepted the events anonymously.
            // ES: Una key en blanco es alguien que seteó la variable y la dejó vacía — eso es "sin
            //     key", no una key hecha de espacios, y mandarla tal cual se ganaría un 401 de un
            //     Seq que si no habría aceptado los eventos anónimos.
            var apiKey = configuration.GetValue<string>($"{Section}:ApiKey");

            return new SeqSinkSettings
            {
                ServerUrl = serverUrl.Trim(),
                ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim(),
            };
        }
    }
}
