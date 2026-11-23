// <copyright file="DbCacheConfigurationCheck.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// EN: Says out loud, at startup, what the repository cache assumes about the deployment.
    ///     <c>IMemoryCache</c> lives inside the process: with two replicas each one caches on its
    ///     own, and a write only invalidates the replica that served it — the other keeps answering
    ///     with data up to 20 minutes old, which is the absolute expiration. That failure is
    ///     invisible from inside the app: nothing crashes, nothing is logged, a user just sees a
    ///     stale value depending on which replica took the request.
    ///     Nothing in the process can count its own replicas, so the deployment declares them in
    ///     <c>DbCacheReplicas</c> and this check turns that declaration into a log line.
    /// ES: Dice en voz alta, al arrancar, qué asume la cache del repositorio sobre el despliegue.
    ///     <c>IMemoryCache</c> vive dentro del proceso: con dos réplicas cada una cachea por su
    ///     cuenta, y una escritura solo invalida la réplica que la atendió — la otra sigue
    ///     respondiendo con datos de hasta 20 minutos, que es la expiración absoluta. Esa falla es
    ///     invisible desde adentro de la app: no rompe nada, no loguea nada, y un usuario ve un valor
    ///     viejo según qué réplica le tocó.
    ///     Ningún proceso puede contar sus propias réplicas, así que el despliegue las declara en
    ///     <c>DbCacheReplicas</c> y este chequeo convierte esa declaración en una línea de log.
    /// </summary>
    public sealed class DbCacheConfigurationCheck(IConfiguration configuration, ILogger<DbCacheConfigurationCheck> logger) : IHostedService
    {
        /// <summary>Writes the check to the log. / Escribe el chequeo en el log.</summary>
        /// <param name="cancellationToken">Cancellation token. / Token de cancelación.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // EN: An app that never turned the cache on has nothing to read about it here.
            // ES: Una app que nunca encendió la cache no tiene nada que leer acá.
            if (!configuration.GetSection("DbCacheIsEnabled").Exists()
                || !configuration.GetValue<bool>("DbCacheIsEnabled"))
            {
                return Task.CompletedTask;
            }

            var replicas = configuration.GetValue<int?>("DbCacheReplicas") ?? 1;

            if (replicas > 1)
            {
                logger.LogWarning(
                    "The repository cache is per-process and this deployment declares {Replicas} replicas: each one caches on its own and a write only invalidates the replica that served it, so the others can answer with data up to 20 minutes old. Set 'DbCacheIsEnabled' to false while there is more than one replica.",
                    replicas);

                return Task.CompletedTask;
            }

            // EN: Information, not Warning: warning on every healthy single-replica deployment is
            //     noise, and noise is how warnings stop being read. The point is that the assumption
            //     is written down on every boot, so whoever scales the deployment reads it.
            // ES: Information, no Warning: avisar en todo despliegue sano de una réplica es ruido, y
            //     el ruido es cómo los warnings dejan de leerse. Lo que importa es que la suposición
            //     quede escrita en cada arranque, para que la lea quien escale el despliegue.
            logger.LogInformation(
                "The repository cache is on. It is per-process, so it assumes a single replica; if this deployment is scaled out, set 'DbCacheIsEnabled' to false or declare 'DbCacheReplicas'.");

            return Task.CompletedTask;
        }

        /// <summary>Nothing to stop. / No hay nada que detener.</summary>
        /// <param name="cancellationToken">Cancellation token. / Token de cancelación.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
