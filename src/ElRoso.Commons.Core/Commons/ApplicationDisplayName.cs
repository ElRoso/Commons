// <copyright file="ApplicationDisplayName.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Commons
{
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// EN: Resolves the name this application shows to a human — the Swagger title and the
    ///     <c>ApplicationName</c> field of every log line.
    ///     🔴 It lives in its own key because <c>ApplicationName</c> is RESERVED by the ASP.NET Core
    ///     host, which reads it as the name of the application assembly. From
    ///     <c>appsettings.json</c> that is harmless — by the time it is read the host is already
    ///     built — but passed on the command line (<c>--ApplicationName=Whatever</c>) the host tries
    ///     to load an assembly called "Whatever" and the app does not start at all.
    ///     <c>ApplicationDisplayName</c> is ours and can be set from anywhere. The reserved key is
    ///     still read as a fallback, because that is what every deployment has in its settings today.
    /// ES: Resuelve el nombre con el que esta aplicación se muestra a una persona — el título de
    ///     Swagger y el campo <c>ApplicationName</c> de cada línea de log.
    ///     🔴 Vive en una clave propia porque <c>ApplicationName</c> es una clave RESERVADA del host
    ///     de ASP.NET Core, que la lee como el nombre del assembly de la aplicación. Desde
    ///     <c>appsettings.json</c> es inofensivo — cuando se lee, el host ya está armado — pero
    ///     pasada por línea de comandos (<c>--ApplicationName=LoQueSea</c>) el host intenta cargar un
    ///     assembly llamado "LoQueSea" y la app directamente no arranca.
    ///     <c>ApplicationDisplayName</c> es nuestra y se puede setear desde donde sea. La clave
    ///     reservada se sigue leyendo como respaldo, porque es la que hoy tienen todos los
    ///     despliegues en su configuración.
    /// </summary>
    public static class ApplicationDisplayName
    {
        /// <summary>The key to use. / La clave que hay que usar.</summary>
        public const string Key = "ApplicationDisplayName";

        /// <summary>The reserved key, still read for compatibility. / La clave reservada, que se sigue leyendo por compatibilidad.</summary>
        public const string ReservedKey = "ApplicationName";

        /// <summary>
        /// Resolves the display name, preferring <see cref="Key"/> over <see cref="ReservedKey"/>.
        /// ES: Resuelve el nombre visible, prefiriendo <see cref="Key"/> sobre <see cref="ReservedKey"/>.
        /// </summary>
        /// <param name="configuration">Application configuration. / Configuración de la aplicación.</param>
        /// <param name="fallback">Value when neither key is set. / Valor cuando no está ninguna de las dos.</param>
        /// <returns>The display name. / El nombre visible.</returns>
        public static string Resolve(IConfiguration configuration, string fallback)
        {
            var name = configuration.GetValue<string>(Key);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            name = configuration.GetValue<string>(ReservedKey);

            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }
    }
}
