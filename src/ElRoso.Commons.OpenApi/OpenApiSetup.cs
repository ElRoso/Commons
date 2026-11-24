// <copyright file="OpenApiSetup.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.OpenApi;

using Asp.Versioning;
using ElRoso.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registra y mapea documentos OpenAPI nativos, uno por versión de API.
/// </summary>
public static class OpenApiSetup
{
    /// <summary>
    /// Registra la generación nativa de OpenAPI sobre el versionado de API.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <param name="schemes">Esquemas de seguridad a publicar.</param>
    /// <returns>La misma colección de servicios.</returns>
    public static IServiceCollection AddElRosoOpenApi(
        this IServiceCollection services,
        IConfiguration configuration,
        OpenApiAuthSchemes schemes = OpenApiAuthSchemes.Bearer)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!IsVersioningRegistered(services))
        {
            throw new InvalidOperationException(
                $"{nameof(ConfigureServices.AddApiVersioningCustom)} must be registered before AddElRosoOpenApi.");
        }

        services.AddApiVersioningCustom(
            configuration,
            builder => builder.AddApiExplorer().AddOpenApi(options =>
                options.Document.AddDocumentTransformer(new OpenApiDocumentTransformer(configuration, schemes))));

        return services;
    }

    /// <summary>
    /// Mapea el endpoint que sirve un documento OpenAPI por versión de API.
    /// </summary>
    /// <param name="endpoints">Constructor de rutas de endpoints.</param>
    /// <param name="pattern">Patrón de ruta con un marcador de nombre de documento.</param>
    /// <returns>El mismo constructor de rutas de endpoints.</returns>
    public static IEndpointRouteBuilder MapElRosoOpenApi(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/openapi/{documentName}.json")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        endpoints.MapOpenApi(pattern).WithDocumentPerVersion();

        return endpoints;
    }

    private static bool IsVersioningRegistered(IServiceCollection services) =>
        services.Any(descriptor => descriptor.ServiceType == typeof(IApiVersionReader));
}
