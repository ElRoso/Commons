// <copyright file="DomainExceptionStatus.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Presentation.Middlewares
{
    using System.Net;
    using ElRoso.Exceptions;

    /// <summary>
    /// Mapea las excepciones de dominio a su status HTTP y distingue las desconocidas.
    /// </summary>
    internal static class DomainExceptionStatus
    {
        /// <summary>
        /// Devuelve el status al que mapea la excepción, o null si no es de dominio.
        /// </summary>
        /// <param name="exception">La excepción a clasificar.</param>
        /// <returns>El status HTTP, o null para excepciones desconocidas.</returns>
        // El match es por herencia: FileNotFoundException hereda de NotFoundException.
        public static HttpStatusCode? MapDomainStatus(Exception exception) => exception switch
        {
            NoContentException => HttpStatusCode.NoContent,
            BadRequestException => HttpStatusCode.BadRequest,
            UnauthorizedException => HttpStatusCode.Unauthorized,
            ForbiddenException => HttpStatusCode.Forbidden,
            NotFoundException => HttpStatusCode.NotFound,
            NotImplementedException => HttpStatusCode.NotImplemented,
            _ => null,
        };

        /// <summary>
        /// Indica si la excepción es de dominio y su mensaje es seguro para quien llamó.
        /// </summary>
        /// <param name="exception">La excepción a clasificar.</param>
        /// <returns>True si la excepción es de dominio.</returns>
        public static bool IsDomainException(Exception exception) => MapDomainStatus(exception) is not null;
    }
}
