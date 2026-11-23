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
    /// EN: Global exception-handling middleware: catches unhandled exceptions, logs them and writes
    ///     an RFC 7807 <c>application/problem+json</c> response. Error messages are hidden in
    ///     non-development environments.
    ///     🔴 It does NOT touch the application cache. It used to flush the whole IMemoryCache on
    ///     every unhandled exception, and there is only one per process: consuming apps keep
    ///     security counters in it (a login throttle per email, permission stamps), so anyone able
    ///     to trigger a 500 could reset them, and every tenant's cached listings reloaded at once.
    ///     Invalidating a bad entry is the job of whoever wrote it, which is the only place that
    ///     knows the key.
    /// ES: Middleware global de excepciones: captura las excepciones no manejadas, las loguea y
    ///     escribe una respuesta RFC 7807 <c>application/problem+json</c>. Los mensajes de error se
    ///     ocultan fuera de Development.
    ///     🔴 NO toca la caché de la aplicación. Antes vaciaba el IMemoryCache entero ante cada
    ///     excepción no manejada, y hay uno solo por proceso: las apps consumidoras guardan ahí
    ///     contadores de seguridad (un throttle de login por email, permission stamps), así que
    ///     cualquiera capaz de disparar un 500 podía resetearlos, y encima los listados cacheados de
    ///     todos los tenants recargaban a la vez. Invalidar una entrada en mal estado es tarea de
    ///     quien la escribió, que es el único que conoce la clave.
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
        /// EN: Kept so consuming apps that build the middleware by hand keep compiling. The cache is
        ///     ignored: nothing here purges it any more — see the class remarks.
        /// ES: Se conserva para que las apps consumidoras que lo construyen a mano sigan compilando.
        ///     La caché se ignora: acá ya no se purga nada — ver las notas de la clase.
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
                    if (IsDomainException(ex))
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

        /// <summary>
        /// EN: The HTTP status a domain exception maps to, or <c>null</c> when the exception is not
        ///     one of ours and therefore means an internal error.
        ///     🔴 Matching is by inheritance, not by exact type. It used to compare types exactly,
        ///     so every subclass fell through to 500 in silence — that is how a missing blob, which
        ///     throws a subclass of NotFoundException, came back as an internal server error with
        ///     its message masked in production.
        /// ES: El status HTTP al que mapea una excepción de dominio, o <c>null</c> cuando la
        ///     excepción no es nuestra y por lo tanto significa un error interno.
        ///     🔴 El match es por herencia, no por tipo exacto. Antes comparaba tipos exactos, así
        ///     que toda subclase caía a 500 en silencio — así es como un blob inexistente, que lanza
        ///     una subclase de NotFoundException, volvía como error interno del servidor con el
        ///     mensaje enmascarado en producción.
        /// </summary>
        private static HttpStatusCode? MapDomainStatus(Exception exception) => exception switch
        {
            NoContentException => HttpStatusCode.NoContent,
            BadRequestException => HttpStatusCode.BadRequest,
            UnauthorizedException => HttpStatusCode.Unauthorized,
            ForbiddenException => HttpStatusCode.Forbidden,

            // EN: FileNotFoundException derives from NotFoundException, so it lands here too.
            // ES: FileNotFoundException hereda de NotFoundException, así que también cae acá.
            NotFoundException => HttpStatusCode.NotFound,
            NotImplementedException => HttpStatusCode.NotImplemented,
            _ => null,
        };

        /// <summary>
        /// Returns true for known domain exceptions whose messages are intentionally user-facing.
        /// Domain exceptions map to 4xx responses; unknown exceptions map to 500.
        /// </summary>
        private static bool IsDomainException(Exception exception) => MapDomainStatus(exception) is not null;

        private async Task HandleExceptionAsync(HttpContext httpContext, Exception exception)
        {
            // Domain exceptions carry intentional user-facing messages — safe to surface in all environments.
            // Unknown exceptions may leak internal details — mask them outside Development.
            // Las excepciones de dominio tienen mensajes pensados para el usuario — siempre se muestran.
            // Las excepciones desconocidas pueden exponer detalles internos — se ocultan fuera de Development.
            var showMessage = IsDomainException(exception) || this.environment.IsDevelopment();

            var problem = new ProblemResponse
            {
                Title = showMessage ? exception.Message : "An error has occurred. Please contact support.",
            };

            problem.Status = MapDomainStatus(exception) ?? HttpStatusCode.InternalServerError;

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
