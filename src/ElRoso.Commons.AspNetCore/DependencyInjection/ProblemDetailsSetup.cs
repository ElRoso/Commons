// <copyright file="ProblemDetailsSetup.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using System;
    using System.Diagnostics;
    using ElRoso.Presentation.Middlewares;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Registra y monta el pipeline de respuestas Problem Details.
    /// </summary>
    public static class ProblemDetailsSetup
    {
        /// <summary>
        /// Registra los servicios de Problem Details y del manejador de excepciones de dominio.
        /// </summary>
        /// <param name="services">Colección de servicios.</param>
        /// <returns>La misma colección de servicios.</returns>
        public static IServiceCollection AddElRosoProblemDetails(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddProblemDetails(options =>
            {
                options.CustomizeProblemDetails = context =>
                {
                    var problemDetails = context.ProblemDetails;
                    problemDetails.Type ??= "about:blank";
                    problemDetails.Instance ??= context.HttpContext.Request.Path.Value;
                    problemDetails.Title ??= ReasonPhrases.GetReasonPhrase(
                        problemDetails.Status ?? StatusCodes.Status500InternalServerError);
                    problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
                };
            });
            services.AddExceptionHandler<DomainExceptionHandler>();
            services.AddSingleton(new ProblemDetailsRegistration());

            return services;
        }

        /// <summary>
        /// Monta el manejo de excepciones y las páginas de status como Problem Details.
        /// </summary>
        /// <param name="app">Builder de la aplicación.</param>
        /// <returns>El mismo builder de la aplicación.</returns>
        public static IApplicationBuilder UseElRosoProblemDetails(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            if (app.ApplicationServices.GetService<ProblemDetailsRegistration>() is null)
            {
                throw new InvalidOperationException(
                    "UseElRosoProblemDetails requires AddElRosoProblemDetails. Call AddElRosoProblemDetails on the service collection before building the application.");
            }

            app.UseExceptionHandler();
            app.UseStatusCodePages();

            return app;
        }

        /// <summary>
        /// Marca el registro de los servicios de Problem Details.
        /// </summary>
        internal sealed class ProblemDetailsRegistration
        {
        }
    }
}
