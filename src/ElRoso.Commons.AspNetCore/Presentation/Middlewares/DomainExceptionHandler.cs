// <copyright file="DomainExceptionHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Presentation.Middlewares
{
    using System.Net;
    using ElRoso.Exceptions;
    using Microsoft.AspNetCore.Diagnostics;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Maneja las excepciones no capturadas y escribe una respuesta RFC 9457.
    /// </summary>
    internal sealed class DomainExceptionHandler : IExceptionHandler
    {
        private const string HiddenMessage = "An error has occurred. Please contact support.";

        private readonly IHostEnvironment environment;
        private readonly ILogger<DomainExceptionHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainExceptionHandler"/> class.
        /// </summary>
        /// <param name="environment">Entorno del host.</param>
        /// <param name="logger">Registro de eventos.</param>
        public DomainExceptionHandler(IHostEnvironment environment, ILogger<DomainExceptionHandler> logger)
        {
            this.environment = environment;
            this.logger = logger;
        }

        /// <summary>
        /// Intenta manejar la excepción y escribir la respuesta correspondiente.
        /// </summary>
        /// <param name="httpContext">Contexto del request.</param>
        /// <param name="exception">Excepción no manejada.</param>
        /// <param name="cancellationToken">Token de cancelación.</param>
        /// <returns>True cuando la excepción fue manejada.</returns>
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var isDomain = DomainExceptionStatus.IsDomainException(exception);
            var status = DomainExceptionStatus.MapDomainStatus(exception) ?? HttpStatusCode.InternalServerError;

            // Las excepciones de dominio van en Warning; las desconocidas conservan Error y stack trace.
            if (isDomain)
            {
                this.logger.LogWarning("[{Path}] {Message}", httpContext.Request.Path.Value, exception.Message);
            }
            else
            {
                this.logger.LogError(exception, "An unhandled exception has occurred.");
            }

            if (status == HttpStatusCode.NoContent)
            {
                httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
                return true;
            }

            var problemDetails = new ProblemDetails
            {
                Status = (int)status,
                Title = ReasonPhrases.GetReasonPhrase((int)status),
                Detail = isDomain || this.environment.IsDevelopment() ? exception.Message : HiddenMessage,
            };

            if (exception is BadRequestException badRequest && badRequest.Details.Count > 0)
            {
                problemDetails.Extensions["errors"] = badRequest.Details;
            }

            httpContext.Response.StatusCode = (int)status;
            var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = problemDetails,
            });

            return true;
        }
    }
}
