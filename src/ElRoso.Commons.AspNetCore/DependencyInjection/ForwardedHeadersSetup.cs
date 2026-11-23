// <copyright file="ForwardedHeadersSetup.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.HttpOverrides;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using System.Collections.Generic;

    /// <summary>
    /// Reescribe RemoteIpAddress desde X-Forwarded-For confiando en un solo salto (ForwardLimit = 1) detrás de un reverse proxy.
    /// Redes confiables: loopback, RFC 1918 y ForwardedHeaders:KnownNetworks; una entrada ilegible lanza excepción al registrar.
    /// </summary>
    public static class ForwardedHeadersSetup
    {
        /// <summary>
        /// EN: Comma-separated CIDR list of the proxies whose X-Forwarded-For we believe. Absent or
        ///     blank means the defaults; an unreadable entry is a startup error.
        /// ES: Lista de CIDR separados por coma de los proxies a los que les creemos el
        ///     X-Forwarded-For. Ausente o vacío cae a los defaults; una entrada ilegible es un error
        ///     de arranque.
        /// </summary>
        public const string KnownNetworksKey = "ForwardedHeaders:KnownNetworks";

        private const string DefaultNetworks = "127.0.0.0/8,::1/128,10.0.0.0/8,172.16.0.0/12,192.168.0.0/16";

        /// <summary>
        /// EN: Configures the forwarded-headers policy. Pair it with UseElRosoForwardedHeaders.
        /// ES: Configura la política de forwarded headers. Va junto con UseElRosoForwardedHeaders.
        /// </summary>
        public static IServiceCollection AddElRosoForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
        {
            // EN: Parsed here and not inside the Configure callback so an unreadable value fails at
            //     registration, while the deploy is still on screen — not lazily, whenever something
            //     first happens to read an address.
            // ES: Se parsea acá y no adentro del callback de Configure para que un valor ilegible
            //     falle al registrar, con el deploy todavía en pantalla — y no en diferido, cuando
            //     algo lea una dirección por primera vez.
            var trustedNetworks = TrustedNetworks(configuration);

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                    | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;

                // EN: One hop — see the class remarks. This is the anti-forgery property, not a tuning knob.
                // ES: Un salto — ver las notas de la clase. Es la propiedad anti-falsificación, no una perilla.
                options.ForwardLimit = 1;

                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();

                foreach (var network in trustedNetworks)
                {
                    options.KnownIPNetworks.Add(network);
                }
            });

            return services;
        }

        /// <summary>
        /// EN: 🔴 Must run FIRST in the pipeline, before anything reads the caller's address.
        /// ES: 🔴 Tiene que correr PRIMERO en el pipeline, antes de que nadie lea la dirección.
        /// </summary>
        public static IApplicationBuilder UseElRosoForwardedHeaders(this IApplicationBuilder app)
        {
            return app.UseForwardedHeaders();
        }

        /// <summary>
        /// EN: The networks to trust. An absent or blank setting falls back to the defaults — that is
        ///     the documented "not configured" case. An entry that cannot be parsed is NOT: it is a
        ///     deployment error and it throws.
        ///     🔴 Skipping it silently is what this used to do, and the cost is not "one network
        ///     less": the single configured network IS the proxy, so dropping it leaves
        ///     X-Forwarded-For ignored and every caller looking like the proxy — the 2026-08-03
        ///     login outage, reachable from one typo in a variable group, with no error, no log and
        ///     a green health check.
        /// ES: Las redes a confiar. Un setting ausente o en blanco cae a los defaults — es el caso
        ///     documentado de "sin configurar". Una entrada que no parsea NO lo es: es un error de
        ///     despliegue y lanza excepción.
        ///     🔴 Saltearla en silencio es lo que hacía antes, y el costo no es "una red menos": la
        ///     única red configurada ES la del proxy, así que descartarla deja el X-Forwarded-For
        ///     ignorado y a todos los que llaman pareciendo el proxy — la caída de login del
        ///     2026-08-03, alcanzable con un dedazo en un variable group, sin error, sin log y con
        ///     el health check en verde.
        /// </summary>
        /// <param name="configuration">EN: Configuration to read the setting from. / ES: Configuración de donde leer el setting.</param>
        /// <returns>EN: The parsed networks. / ES: Las redes parseadas.</returns>
        /// <exception cref="System.InvalidOperationException">EN: An entry is not a readable CIDR. / ES: Una entrada no es un CIDR legible.</exception>
        private static IReadOnlyList<System.Net.IPNetwork> TrustedNetworks(IConfiguration configuration)
        {
            var configured = configuration?[KnownNetworksKey];
            var source = string.IsNullOrWhiteSpace(configured) ? DefaultNetworks : configured;

            var networks = new List<System.Net.IPNetwork>();

            foreach (var entry in source.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries))
            {
                if (!System.Net.IPNetwork.TryParse(entry, out var parsed))
                {
                    throw new System.InvalidOperationException(
                        $"{KnownNetworksKey} contiene una red ilegible: '{entry}'. " +
                        "Sin ella el proxy deja de ser confiable, se ignora el X-Forwarded-For y los " +
                        "rate limiters de autenticación dejan de particionar por dirección.");
                }

                networks.Add(parsed);
            }

            return networks;
        }
    }
}
