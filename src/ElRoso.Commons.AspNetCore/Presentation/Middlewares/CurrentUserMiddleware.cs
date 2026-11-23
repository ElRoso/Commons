// <copyright file="CurrentUserMiddleware.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Presentation.Middlewares
{
    using ElRoso.Domains.Session;
    using ElRoso.Exceptions;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Middleware that extracts the authenticated user's identity claims and stores a
    /// <see cref="CurrentUser"/> instance in <c>HttpContext.Items["currentUser"]</c>
    /// for downstream consumption by controllers and repositories.
    /// Throws <see cref="UnauthorizedException"/> if any required claim is missing.
    /// </summary>
    public class CurrentUserMiddleware : IMiddleware
    {
        private readonly ILogger<CurrentUserMiddleware> logger;

        public CurrentUserMiddleware(ILogger<CurrentUserMiddleware> logger)
        {
            this.logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            using (this.logger.BeginScope(context.Request.Path))
            {
                var user = context.User;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    this.logger.LogInformation("User is authenticated.");

                    var objectId = user.Claims
                        .FirstOrDefault(c => c.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
                    var surname = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Surname)?.Value;
                    var givenName = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value;
                    var email = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Upn)?.Value;

                    if (string.IsNullOrEmpty(objectId) || string.IsNullOrEmpty(surname)
                        || string.IsNullOrEmpty(givenName) || string.IsNullOrEmpty(email))
                    {
                        this.logger.LogWarning("Required claims missing in authenticated token.");
                        throw new UnauthorizedException();
                    }

                    // EN: A malformed identifier is a bad token, not a server fault. Guid.Parse threw
                    //     FormatException, which is not a domain exception — so it came back as a 500
                    //     with the message masked in production, hiding the real cause from whoever
                    //     had to debug it.
                    // ES: Un identificador mal formado es un token malo, no una falla del servidor.
                    //     Guid.Parse tiraba FormatException, que no es excepción de dominio — así que
                    //     volvía como 500 con el mensaje enmascarado en producción, escondiéndole la
                    //     causa real a quien tenía que depurarlo.
                    if (!Guid.TryParse(objectId, out var userId))
                    {
                        this.logger.LogWarning("Claim '{Claim}' is not a valid GUID.", "objectidentifier");
                        throw new UnauthorizedException();
                    }

                    var currentUser = new CurrentUser
                    {
                        Id = userId,
                        Surname = surname,
                        GivenName = givenName,
                        EmailAddress = email,
                    };

                    this.logger.LogInformation("Set user in httpContext.");
                    context.Items["currentUser"] = currentUser;
                }

                await next.Invoke(context);
            }
        }
    }
}
