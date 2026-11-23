// <copyright file="HealthCheckSetup.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using ElRoso.Domains.Responses;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Diagnostics.HealthChecks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// EN: The health check endpoint. <c>CreateBuilder</c> registers the health check *services*,
    ///     but until 2026-08-05 nothing mapped a route, so every consumer had to write the endpoint
    ///     by hand — and the <see cref="HealthCheckResponse"/> DTOs sat in the library with no
    ///     producer. An app with no health endpoint is an app the orchestrator cannot tell apart
    ///     from a healthy one.
    /// ES: El endpoint de health check. <c>CreateBuilder</c> registra los *servicios* de health
    ///     check, pero hasta el 2026-08-05 nadie mapeaba una ruta, así que cada consumidor tenía que
    ///     escribir el endpoint a mano — y los DTOs <see cref="HealthCheckResponse"/> vivían en la
    ///     librería sin ningún productor. Una app sin endpoint de salud es una app que el
    ///     orquestador no puede distinguir de una sana.
    /// </summary>
    public static class HealthCheckSetup
    {
        /// <summary>
        /// EN: Maps the health endpoint, answering the library's own <see cref="HealthCheckResponse"/>
        ///     shape: overall status, one entry per registered check, and how long the whole thing
        ///     took. Returns 200 when healthy and 503 otherwise, which is what an orchestrator reads.
        /// ES: Mapea el endpoint de salud, respondiendo con la forma propia de la librería
        ///     (<see cref="HealthCheckResponse"/>): estado general, una entrada por chequeo
        ///     registrado, y cuánto tardó todo. Devuelve 200 si está sano y 503 si no, que es lo que
        ///     lee un orquestador.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder. / El builder de rutas.</param>
        /// <param name="pattern">Route, <c>/healthz</c> by default. / Ruta, <c>/healthz</c> por defecto.</param>
        /// <returns>The mapped endpoint convention builder.</returns>
        public static IEndpointConventionBuilder MapHealthChecksCustom(
            this IEndpointRouteBuilder endpoints,
            string pattern = "/healthz")
        {
            return endpoints.MapHealthChecks(pattern, new HealthCheckOptions
            {
                ResponseWriter = WriteResponseAsync,
            });
        }

        /// <summary>
        /// EN: Same as <see cref="MapHealthChecksCustom"/>, for apps that build the pipeline with
        ///     <c>IApplicationBuilder</c> instead of mapping endpoints directly.
        /// ES: Igual que <see cref="MapHealthChecksCustom"/>, para apps que arman el pipeline con
        ///     <c>IApplicationBuilder</c> en vez de mapear endpoints directamente.
        /// </summary>
        /// <param name="app">The application builder. / El builder de la aplicación.</param>
        /// <param name="pattern">Route, <c>/healthz</c> by default. / Ruta, <c>/healthz</c> por defecto.</param>
        /// <returns>The same application builder, for chaining.</returns>
        public static IApplicationBuilder UseHealthChecksCustom(
            this IApplicationBuilder app,
            string pattern = "/healthz")
        {
            return app.UseHealthChecks(pattern, new HealthCheckOptions
            {
                ResponseWriter = WriteResponseAsync,
            });
        }

        private static Task WriteResponseAsync(HttpContext context, HealthReport report)
        {
            var response = new HealthCheckResponse
            {
                Status = report.Status.ToString(),
                HealthCheckDuration = report.TotalDuration,
                HealthChecks = report.Entries.Select(entry => new IndividualHealthCheckResponse
                {
                    Component = entry.Key,
                    Status = entry.Value.Status.ToString(),

                    // EN: 🔴 When a check throws, ASP.NET Core puts the EXCEPTION MESSAGE into
                    //     Description. This endpoint is usually reachable without authentication, so
                    //     publishing it verbatim leaks whatever the exception carried — a connection
                    //     string with its password, an internal host, a stack trace. Descriptions
                    //     written by hand are still published; the ones that came from an exception
                    //     are replaced. Verified with a test that fails if this is removed.
                    // ES: 🔴 Cuando un chequeo lanza, ASP.NET Core mete el MENSAJE DE LA EXCEPCIÓN en
                    //     Description. Este endpoint suele ser accesible sin autenticar, así que
                    //     publicarlo tal cual filtra lo que la excepción traiga — un connection
                    //     string con su contraseña, un host interno, un stack trace. Las
                    //     descripciones escritas a mano se siguen publicando; las que vinieron de una
                    //     excepción se reemplazan. Verificado con un test que falla si se saca.
                    Description = entry.Value.Exception is not null
                        ? "The check failed. See the application logs."
                        : entry.Value.Description ?? string.Empty,
                }).ToList(),
            };

            context.Response.ContentType = "application/json";
            return context.Response.WriteAsJsonAsync(response);
        }
    }
}
