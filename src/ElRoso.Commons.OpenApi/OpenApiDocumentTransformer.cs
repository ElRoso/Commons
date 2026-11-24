// <copyright file="OpenApiDocumentTransformer.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.OpenApi;

using ElRoso.Commons;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi;

/// <summary>
/// Aplica metadatos comunes y esquemas de seguridad a cada documento OpenAPI versionado.
/// </summary>
internal sealed class OpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string DeprecationNotice = "This API version has been deprecated.";
    private static readonly Uri DefaultLicenseUrl = new("https://opensource.org/licenses/MIT");

    private readonly IConfiguration configuration;
    private readonly OpenApiAuthSchemes schemes;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiDocumentTransformer"/> class.
    /// ES: Crea un transformador para los esquemas de seguridad elegidos.
    /// </summary>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <param name="schemes">Esquemas de seguridad a publicar.</param>
    public OpenApiDocumentTransformer(IConfiguration configuration, OpenApiAuthSchemes schemes)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        this.configuration = configuration;
        this.schemes = schemes;
    }

    /// <summary>
    /// Establece el título, la licencia, el aviso de deprecación y los requisitos de seguridad de un documento.
    /// </summary>
    /// <param name="document">Documento OpenAPI a transformar.</param>
    /// <param name="context">Contexto de la transformación.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Una tarea completada.</returns>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        var info = document.Info;
        info.Title = ApplicationDisplayName.Resolve(this.configuration, "ElRoso");
        info.License = new OpenApiLicense { Name = "MIT", Url = ResolveLicenseUrl(this.configuration) };

        if (IsDeprecated(context))
        {
            var description = info.Description ?? string.Empty;

            if (!description.Contains("deprecated", StringComparison.OrdinalIgnoreCase))
            {
                info.Description = description.Length == 0
                    ? DeprecationNotice
                    : $"{description} {DeprecationNotice}";
            }
        }

        if (this.schemes == OpenApiAuthSchemes.None)
        {
            return Task.CompletedTask;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Security ??= [];

        var requirement = new OpenApiSecurityRequirement();

        if ((this.schemes & OpenApiAuthSchemes.Bearer) != 0)
        {
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
            };

            requirement[new OpenApiSecuritySchemeReference("Bearer", document)] = [];
        }

        if ((this.schemes & OpenApiAuthSchemes.Basic) != 0)
        {
            document.Components.SecuritySchemes["Basic"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "basic",
            };

            requirement[new OpenApiSecuritySchemeReference("Basic", document)] = [];
        }

        document.Security.Add(requirement);

        return Task.CompletedTask;
    }

    private static bool IsDeprecated(OpenApiDocumentTransformerContext context) =>
        context.DescriptionGroups
            .Where(group => string.Equals(group.GroupName, context.DocumentName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(group => group.Items)
            .Any(description => description.IsDeprecated);

    private static Uri ResolveLicenseUrl(IConfiguration configuration)
    {
        var configured = configuration.GetValue<string>("OpenApi:LicenseUrl");

        return configured is not null && Uri.TryCreate(configured, UriKind.Absolute, out var parsed)
            ? parsed
            : DefaultLicenseUrl;
    }
}
