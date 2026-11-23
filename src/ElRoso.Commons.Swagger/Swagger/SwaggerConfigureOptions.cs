// <copyright file="SwaggerConfigureOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Swagger;

using Asp.Versioning.ApiExplorer;
using ElRoso.Commons;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

internal class SwaggerConfigureOptions(IConfiguration configuration, IApiVersionDescriptionProvider apiVersionDescriptionProvider)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var desc in apiVersionDescriptionProvider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(desc.GroupName, this.CreateInfoForApiVersion(desc));
        }
    }

    // MIT license URL — configurable via "Swagger:LicenseUrl" to avoid hardcoded URIs.
    // URL de licencia MIT — configurable via "Swagger:LicenseUrl" para evitar URIs hardcodeadas.
    private static readonly Uri DefaultLicenseUrl = new("https://opensource.org/licenses/MIT");

    private OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
    {
        var applicationName = ApplicationDisplayName.Resolve(configuration, fallback: "ElRoso");

        var licenseUrlString = configuration.GetValue<string>("Swagger:LicenseUrl");
        var licenseUrl = licenseUrlString is not null && Uri.TryCreate(licenseUrlString, UriKind.Absolute, out var parsed)
            ? parsed
            : DefaultLicenseUrl;

        var info = new OpenApiInfo()
        {
            Title = applicationName,
            Version = description.ApiVersion.ToString(),
            License = new OpenApiLicense { Name = "MIT", Url = licenseUrl },
        };

        if (description.IsDeprecated)
        {
            info.Description += " This API version has been deprecated.";
        }

        return info;
    }
}
