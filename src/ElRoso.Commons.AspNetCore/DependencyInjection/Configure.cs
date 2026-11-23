// <copyright file="Configure.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using ElRoso.Presentation.Middlewares;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Hosting;
    using Microsoft.IdentityModel.Logging;
    using System;
    using System.Reflection;

    public static class Configure
    {
        public static void UseEnableAllCors(this IApplicationBuilder app)
        {
            app.UseCors($"EnableAllCors");
        }

        /// <summary>
        /// EN: Maps the controllers plus an informational <c>GET /</c>.
        ///     🔴 <paramref name="beforeEndpoints"/> is the ONLY place a consumer can insert a
        ///     middleware that needs to run after authentication and before the endpoints — most of
        ///     all <c>UseCurrentUserMiddleware()</c>. Mapping the endpoints terminates the pipeline,
        ///     so anything added after this call never executes; and adding it before the pipeline's
        ///     authentication step leaves the caller unauthenticated, which makes that middleware do
        ///     nothing. Without this hook there was no valid position at all, and the cost was
        ///     silent: <c>CreatedBy</c> and <c>ModifiedBy</c> written as null on every row.
        /// ES: Mapea los controllers más un <c>GET /</c> informativo.
        ///     🔴 <paramref name="beforeEndpoints"/> es el ÚNICO lugar donde un consumidor puede
        ///     insertar un middleware que necesite correr después de la autenticación y antes de los
        ///     endpoints — sobre todo <c>UseCurrentUserMiddleware()</c>. Mapear los endpoints termina
        ///     el pipeline, así que lo que se agregue después de esta llamada nunca se ejecuta; y
        ///     agregarlo antes de la autenticación deja a quien llama sin autenticar, con lo cual ese
        ///     middleware no hace nada. Sin este hook no había ninguna posición válida, y el costo
        ///     era silencioso: <c>CreatedBy</c> y <c>ModifiedBy</c> grabados en null en cada fila.
        /// </summary>
        /// <param name="app">The application builder. / El builder de la aplicación.</param>
        /// <param name="hostEnvironment">Environment, reported by <c>GET /</c>. / Entorno, informado por <c>GET /</c>.</param>
        /// <param name="beforeEndpoints">Runs right before the endpoints are mapped. / Corre justo antes de mapear los endpoints.</param>
        /// <param name="mapExtra">Extra endpoint mappings. / Mapeos de endpoints adicionales.</param>
        public static void UseEndpointsCustom(
            this IApplicationBuilder app,
            IWebHostEnvironment hostEnvironment,
            Action<IApplicationBuilder>? beforeEndpoints = null,
            Action<IEndpointRouteBuilder>? mapExtra = null)
        {
            beforeEndpoints?.Invoke(app);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                endpoints.MapElRosoInfo(hostEnvironment);

                mapExtra?.Invoke(endpoints);
            });
        }

        /// <summary>
        /// Mapea el <c>GET /</c> informativo con entorno, nombre y versión del assembly de entrada.
        /// </summary>
        /// <param name="endpoints">El builder de rutas.</param>
        /// <param name="hostEnvironment">Entorno informado por <c>GET /</c>.</param>
        /// <returns>El builder de convenciones del endpoint.</returns>
        public static IEndpointConventionBuilder MapElRosoInfo(this IEndpointRouteBuilder endpoints, IHostEnvironment hostEnvironment) =>
            endpoints.MapGet("/", async context =>
            {
                var entryAssembly = Assembly.GetEntryAssembly();
                await context.Response.WriteAsJsonAsync($"{hostEnvironment.EnvironmentName}-{entryAssembly?.GetName().Name}: {entryAssembly?.GetName().Version}");
            });

        /// <summary>
        /// Muestra datos personales en los errores de IdentityModel, solo en Development.
        /// </summary>
        /// <param name="app">El builder de la aplicación.</param>
        /// <param name="hostEnvironment">Entorno de hosting.</param>
        /// <returns>El mismo builder de la aplicación.</returns>
        public static IApplicationBuilder UseIdentityModelPIIInDevelopment(this IApplicationBuilder app, IHostEnvironment hostEnvironment)
        {
            if (hostEnvironment.IsDevelopment())
            {
                IdentityModelEventSource.ShowPII = true;
            }

            return app;
        }

        public static void UseCurrentUserMiddleware(this IApplicationBuilder app)
        {
            app.UseMiddleware<CurrentUserMiddleware>();
        }

        public static void UseExceptionHandlerMiddleware(this IApplicationBuilder app)
        {
            app.UseMiddleware<ExceptionHandlerMiddleware>();
        }
    }
}
