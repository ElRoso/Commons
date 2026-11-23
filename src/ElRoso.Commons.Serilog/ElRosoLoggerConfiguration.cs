// <copyright file="ElRosoLoggerConfiguration.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Commons
{
    using Microsoft.Extensions.Configuration;
    using Serilog;
    using Serilog.Events;
    using Serilog.Sinks.Graylog;
    using Serilog.Sinks.SystemConsole.Themes;

    internal class ElRosoLoggerConfiguration : LoggerConfiguration
    {
        public ElRosoLoggerConfiguration(IConfiguration configuration)
        {
            var applicationName = ApplicationDisplayName.Resolve(configuration, fallback: string.Empty);

            this
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ApplicationName", $"{applicationName}")
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithEnvironmentUserName()
                .Enrich.WithThreadName()
                .MinimumLevel.Debug()

                // EN: 🔴 Microsoft.AspNetCore.Hosting.Diagnostics logs the RAW url at Information on
                //     every request ("Request starting / Request finished"). SignalR carries the
                //     user's JWT in the query string —a WebSocket handshake cannot send an
                //     Authorization header— so those two lines wrote a valid session token to the
                //     logs on every hub connection: measured at 57 times in a single browsing
                //     session. The token cannot leave the url; the line is what has to go.
                //     ⚠️ It has to be fixed HERE and nowhere else. AddSerilogCustom calls
                //     ClearProviders() and installs this logger already built, so a consumer's
                //     Microsoft.Extensions.Logging filter rules never run, and this class hardcodes
                //     MinimumLevel.Debug() without ReadFrom.Configuration, so its appsettings has no
                //     say either. A suppression written in the consumer app looks applied and does
                //     nothing.
                //     ⚠️ ONE logger by name, on purpose: silencing "Microsoft" wholesale would take
                //     EF Core's command log and the auth handlers with it. Warning and above still
                //     get through — this hides noise, not failures.
                //     ℹ️ What replaces it is the consumer's own request-logging line, with the query
                //     string scrubbed. An app without one loses its request log: that is the trade,
                //     and it is the right way round, because a log with a live token in it is worse
                //     than no log.
                // ES: 🔴 Microsoft.AspNetCore.Hosting.Diagnostics loguea la url CRUDA en Information
                //     en cada request ("Request starting / Request finished"). SignalR lleva el JWT
                //     del usuario en el query string —un handshake de WebSocket no puede mandar
                //     header Authorization—, así que esas dos líneas escribían un token de sesión
                //     válido en los logs en cada conexión al hub: medido en 57 veces en una sola
                //     sesión de navegación. El token no puede salir de la url; lo que tiene que
                //     salir es la línea.
                //     ⚠️ Hay que arreglarlo ACÁ y en ningún otro lado. AddSerilogCustom hace
                //     ClearProviders() e instala este logger ya construido, así que las reglas de
                //     filtro de Microsoft.Extensions.Logging del consumidor nunca corren, y esta
                //     clase hardcodea MinimumLevel.Debug() sin ReadFrom.Configuration, así que su
                //     appsettings tampoco decide. Una supresión escrita en la app consumidora parece
                //     aplicada y no hace nada.
                //     ⚠️ UN logger por nombre, a propósito: callar "Microsoft" entero se llevaría
                //     puesto el log de comandos de EF Core y los handlers de auth. Warning para
                //     arriba sigue pasando — esto esconde ruido, no fallas.
                //     ℹ️ Lo que lo reemplaza es la línea de request propia del consumidor, con el
                //     query string saneado. Una app que no tenga una pierde su log de requests: ése
                //     es el canje, y está en el sentido correcto, porque un log con un token vivo
                //     adentro es peor que no tener log.
                .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)

                .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug, theme: AnsiConsoleTheme.Code);

            // EN: 🔴 The API key is what makes the difference between a Seq that stores events and
            //     one that answers 401 to every batch while the app logs happily — see
            //     SeqSinkSettings for why that failure is invisible from here. It is optional: a
            //     Seq that ingests anonymously needs none.
            // ES: 🔴 La API key es lo que separa un Seq que guarda eventos de uno que contesta 401
            //     a cada lote mientras la app loguea tan campante — ver SeqSinkSettings para por
            //     qué esa falla es invisible desde acá. Es opcional: un Seq que ingesta anónimo no
            //     necesita ninguna.
            var seq = SeqSinkSettings.Read(configuration);
            if (seq is not null)
            {
                this.WriteTo.Seq(seq.ServerUrl, restrictedToMinimumLevel: LogEventLevel.Warning, apiKey: seq.ApiKey);
            }

            if (configuration.GetSection("Graylog").Exists())
            {
                this.WriteTo.Graylog(new GraylogSinkOptions
                {
                    HostnameOrAddress = configuration.GetValue<string>("Graylog:HostnameOrAddress"),
                    Port = configuration.GetValue<int>("Graylog:Port"),
                    TransportType = Serilog.Sinks.Graylog.Core.Transport.TransportType.Udp,
                });
            }

            // EN: The path used to be hardcoded to /logs/{App} — absolute, outside the app's own
            //     directory and impossible to change. In a container without that volume mounted,
            //     or without write permission, Serilog swallows the failure: the app starts, looks
            //     healthy, and simply has no file log. On Windows it resolves to the root of the
            //     current drive. The default stays the same so nothing changes for deployments that
            //     already mount it.
            // ES: La ruta estaba fija en /logs/{App} — absoluta, fuera del directorio de la app e
            //     imposible de cambiar. En un contenedor sin ese volumen montado, o sin permiso de
            //     escritura, Serilog se traga la falla: la app arranca, parece sana, y simplemente
            //     no tiene log de archivo. En Windows resuelve a la raíz del disco actual. El
            //     default se mantiene para que nada cambie en los despliegues que ya lo montan.
            var logDirectory = configuration.GetValue<string>("Logging:FilePath") ?? $"/logs/{applicationName}";

            this.WriteTo.File(
                System.IO.Path.Combine(logDirectory, ".log"),
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} <s:{SourceContext}>{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Information,
                retainedFileCountLimit: 7,
                rollingInterval: RollingInterval.Day);
        }
    }
}
