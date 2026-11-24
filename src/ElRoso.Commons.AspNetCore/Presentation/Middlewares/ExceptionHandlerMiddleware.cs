// <copyright file="ExceptionHandlerMiddleware.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Presentation.Middlewares
{
    using ElRoso.Domains.Responses;
    using ElRoso.Exceptions;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System.Diagnostics;
    using System.Net;

    /// <summary>
    /// EN: Catches unhandled exceptions, logs them and writes an RFC 7807
    ///     <c>application/problem+json</c> response; messages are hidden outside Development.
    /// ES: Captura las excepciones no manejadas, las registra y escribe una respuesta RFC 7807
    ///     <c>application/problem+json</c>; los mensajes se ocultan fuera de Development.
    /// 🔴 No vacía la caché: los consumidores guardan contadores de seguridad en ella.
    /// </summary>
    public class ExceptionHandlerMiddleware : IMiddleware
    {
        private readonly IHostEnvironment environment;
        private readonly ILogger<ExceptionHandlerMiddleware> logger;

        public ExceptionHandlerMiddleware(IHostEnvironment environment, ILogger<ExceptionHandlerMiddleware> logger)
        {
            this.environment = environment;
            this.logger = logger;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionHandlerMiddleware"/> class.
        /// ES: Inicializa el middleware con el entorno y el registro; la caché no se utiliza.
        /// </summary>
        /// <param name="environment">EN: Host environment. / ES: Entorno del host.</param>
        /// <param name="logger">EN: Logger. / ES: Logger.</param>
        /// <param name="memoryCache">EN: Ignored. / ES: Ignorado.</param>
        [Obsolete("The middleware no longer touches the cache. Use the (IHostEnvironment, ILogger) constructor.")]
        public ExceptionHandlerMiddleware(IHostEnvironment environment, ILogger<ExceptionHandlerMiddleware> logger, IMemoryCache memoryCache)
            : this(environment, logger)
        {
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            using (this.logger.BeginScope(context.Request.Path))
            {
                try
                {
                    context.Response.ContentType = "application/json";
                    await next.Invoke(context);
                }
                catch (Exception ex)
                {
                    // Excepción de dominio en Warning; la desconocida conserva Error y stack trace.
                    if (DomainExceptionStatus.IsDomainException(ex))
                    {
                        this.logger.LogWarning("[{Path}] {Message}", context.Request.Path.Value, ex.Message);
                    }
                    else
                    {
                        this.logger.LogError(ex, "[{Path}] An error has occurred:", context.Request.Path.Value);
                    }

                    await this.HandleExceptionAsync(context, ex);
                }
            }
        }

        private async Task HandleExceptionAsync(HttpContext httpContext, Exception exception)
        {
            // Muestra el mensaje de la falla de dominio o cuando el entorno es Development.
            var showMessage = DomainExceptionStatus.IsDomainException(exception) || this.environment.IsDevelopment();

            var problem = new ProblemResponse
            {
                Title = showMessage ? exception.Message : "An error has occurred. Please contact support.",
            };

            problem.Status = DomainExceptionStatus.MapDomainStatus(exception) ?? HttpStatusCode.InternalServerError;

            if (exception is BadRequestException badRequest)
            {
                problem.Details = badRequest.Details;
            }

            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
            if (traceId != null)
            {
                problem.TraceId = traceId;
            }

            httpContext.Response.StatusCode = (int)problem.Status;

            await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        }
    }
}
