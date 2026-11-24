namespace ElRoso.Commons.Tests;

using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Asp.Versioning;
using ElRoso.DependencyInjection;
using ElRoso.OpenApi;
using ElRoso.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class OpenApiSetupTests
{
    private const string DisplayName = "ApiDePrueba";

    [Fact]
    public async Task EachVersionDocument_IsServedWithTheExpectedMetadata()
    {
        await using var app = await StartAsync();

        foreach (var (document, expectedVersion) in new[] { ("v1", "1.0"), ("v2", "2.0") })
        {
            using var json = await GetDocumentAsync(app, document);
            var root = json.RootElement;

            Assert.StartsWith("3.", Get(root, "openapi").GetString());
            Assert.Equal(DisplayName, Get(root, "info", "title").GetString());
            Assert.Equal(expectedVersion, Get(root, "info", "version").GetString());
        }
    }

    [Theory]
    [InlineData(null, "https://opensource.org/licenses/MIT")]
    [InlineData("https://example.org/license", "https://example.org/license")]
    public async Task EachVersionDocument_UsesTheConfiguredLicenseUrl(string? configured, string expected)
    {
        await using var app = await StartAsync(licenseUrl: configured);

        foreach (var version in new[] { "v1", "v2" })
        {
            using var json = await GetDocumentAsync(app, version);
            var license = Get(json.RootElement, "info", "license");

            Assert.Equal("MIT", Get(license, "name").GetString());
            Assert.Equal(expected, Get(license, "url").GetString());
        }
    }

    [Fact]
    public async Task EachVersionDocument_ContainsOnlyItsOwnRoutes()
    {
        await using var app = await StartAsync();

        using var v1 = await GetDocumentAsync(app, "v1");
        var v1Paths = Get(v1.RootElement, "paths").EnumerateObject().Select(p => p.Name).ToList();

        Assert.Contains("/v1/ping", v1Paths);
        Assert.DoesNotContain("/v2/ping", v1Paths);
        Assert.DoesNotContain("/openapi-test/v2/problem", v1Paths);

        using var v2 = await GetDocumentAsync(app, "v2");
        var v2Paths = Get(v2.RootElement, "paths").EnumerateObject().Select(p => p.Name).ToList();

        Assert.Contains("/v2/ping", v2Paths);
        Assert.Contains("/openapi-test/v2/problem", v2Paths);
        Assert.DoesNotContain("/v1/ping", v2Paths);
    }

    [Fact]
    public async Task DeprecatedVersion_IsMarkedInItsDescription()
    {
        await using var app = await StartAsync();

        using var v1 = await GetDocumentAsync(app, "v1");
        var v1Description = Get(v1.RootElement, "info", "description").GetString();
        Assert.DoesNotContain("deprecated", v1Description, StringComparison.OrdinalIgnoreCase);

        using var v2 = await GetDocumentAsync(app, "v2");
        var v2Description = Get(v2.RootElement, "info", "description").GetString();
        Assert.Contains("deprecated", v2Description, StringComparison.OrdinalIgnoreCase);
        var deprecatedCount = Regex.Matches(v2Description ?? string.Empty, "deprecated", RegexOptions.IgnoreCase).Count;
        Assert.Equal(1, deprecatedCount);
    }

    [Fact]
    public async Task DeprecatedMinimalEndpoint_HasSingleNoticeInDescription()
    {
        await using var app = await StartMinimalEndpointsAsync();

        using var v1 = await GetDocumentAsync(app, "v1");
        var v1Description = Get(v1.RootElement, "info", "description").GetString();
        Assert.DoesNotContain("deprecated", v1Description, StringComparison.OrdinalIgnoreCase);

        using var v2 = await GetDocumentAsync(app, "v2");
        var v2Description = Get(v2.RootElement, "info", "description").GetString();
        var deprecatedCount = Regex.Matches(v2Description ?? string.Empty, "deprecated", RegexOptions.IgnoreCase).Count;
        Assert.Equal(1, deprecatedCount);
    }

    [Theory]
    [InlineData(OpenApiAuthSchemes.Bearer, true, false)]
    [InlineData(OpenApiAuthSchemes.Basic, false, true)]
    [InlineData(OpenApiAuthSchemes.Bearer | OpenApiAuthSchemes.Basic, true, true)]
    [InlineData(OpenApiAuthSchemes.None, false, false)]
    public async Task SecuritySchemes_MatchTheRequestedFlags(OpenApiAuthSchemes schemes, bool expectBearer, bool expectBasic)
    {
        await using var app = await StartAsync(schemes: schemes);

        using var json = await GetDocumentAsync(app, "v1");
        var root = json.RootElement;

        if (!expectBearer && !expectBasic)
        {
            if (root.TryGetProperty("components", out var components))
            {
                Assert.False(components.TryGetProperty("securitySchemes", out _));
            }

            if (root.TryGetProperty("security", out var security))
            {
                Assert.Empty(security.EnumerateArray());
            }

            return;
        }

        var schemesInDocument = Get(root, "components", "securitySchemes");

        if (expectBearer)
        {
            var bearer = Get(schemesInDocument, "Bearer");
            Assert.Equal("http", Get(bearer, "type").GetString());
            Assert.Equal("bearer", Get(bearer, "scheme").GetString());
            Assert.Equal("JWT", Get(bearer, "bearerFormat").GetString());
        }
        else
        {
            Assert.False(schemesInDocument.TryGetProperty("Bearer", out _));
        }

        if (expectBasic)
        {
            var basic = Get(schemesInDocument, "Basic");
            Assert.Equal("http", Get(basic, "type").GetString());
            Assert.Equal("basic", Get(basic, "scheme").GetString());
        }
        else
        {
            Assert.False(schemesInDocument.TryGetProperty("Basic", out _));
        }

        var requirements = Get(root, "security")
            .EnumerateArray()
            .SelectMany(requirement => requirement.EnumerateObject().Select(property => property.Name))
            .ToList();

        Assert.Equal(expectBearer, requirements.Contains("Bearer"));
        Assert.Equal(expectBasic, requirements.Contains("Basic"));
    }

    [Fact]
    public async Task ProducesProblem_IsReflectedInTheDocument()
    {
        await using var app = await StartAsync();

        using var json = await GetDocumentAsync(app, "v2");
        var response = Get(json.RootElement, "paths", "/openapi-test/v2/problem", "get", "responses", "404");

        Assert.True(Get(response, "content").TryGetProperty("application/problem+json", out _));
    }

    [Fact]
    public void WithoutApiVersioning_RegistrationExplainsTheRequirement()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var exception = Record.Exception(() => services.AddElRosoOpenApi(ConfigFrom(new())));

        var invalid = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("AddApiVersioningCustom", invalid.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SwaggerAndOpenApi_CanBeServedTogether()
    {
        await using var app = await StartAsync(swagger: true);

        using var swagger = await app.GetTestClient().GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);

        using var openApi = await GetDocumentAsync(app, "v1");
    }

    private static IConfiguration ConfigFrom(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static async Task<JsonDocument> GetDocumentAsync(WebApplication app, string version)
    {
        using var response = await app.GetTestClient().GetAsync($"/openapi/{version}.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static JsonElement Get(JsonElement parent, params string[] path)
    {
        var current = parent;

        foreach (var name in path)
        {
            Assert.Equal(JsonValueKind.Object, current.ValueKind);
            Assert.True(current.TryGetProperty(name, out var next), $"Falta {name} en el documento.");
            current = next;
        }

        return current;
    }

    private static async Task<WebApplication> StartAsync(
        OpenApiAuthSchemes schemes = OpenApiAuthSchemes.None,
        string? licenseUrl = null,
        bool swagger = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
            ApplicationName = typeof(OpenApiSetupTests).Assembly.GetName().Name,
        });

        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationDisplayName"] = DisplayName,
            ["OpenApi:LicenseUrl"] = licenseUrl,
        });

        builder.Services.AddApiVersioningCustom(builder.Configuration);

        if (swagger)
        {
            builder.Services.AddSwaggerJWTAuthentication(builder.Configuration);
        }

        // Solo los controllers de este test entran en los documentos de versión.
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(OpenApiV1Controller).Assembly)
            .ConfigureApplicationPartManager(manager =>
            {
                var defaults = manager.FeatureProviders.OfType<ControllerFeatureProvider>().ToList();
                defaults.ForEach(provider => manager.FeatureProviders.Remove(provider));
                manager.FeatureProviders.Add(new OnlyOpenApiControllers());
            });

        builder.Services.AddElRosoOpenApi(builder.Configuration, schemes);

        var app = builder.Build();

        var v2 = app.NewApiVersionSet().HasApiVersion(new ApiVersion(2, 0)).Build();
        app.MapGet("/openapi-test/v2/problem", () => Results.Ok("v2"))
            .WithApiVersionSet(v2)
            .MapToApiVersion(new ApiVersion(2, 0))
            .ProducesProblem(StatusCodes.Status404NotFound);

        if (swagger)
        {
            app.UseSwaggerCustom();
        }

        app.MapControllers();
        app.MapElRosoOpenApi();

        await app.StartAsync();
        return app;
    }

    private static async Task<WebApplication> StartMinimalEndpointsAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
            ApplicationName = typeof(OpenApiSetupTests).Assembly.GetName().Name,
        });

        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationDisplayName"] = DisplayName,
        });

        builder.Services.AddApiVersioningCustom(builder.Configuration);
        builder.Services.AddElRosoOpenApi(builder.Configuration);

        var app = builder.Build();

        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .HasDeprecatedApiVersion(new ApiVersion(2, 0))
            .ReportApiVersions()
            .Build();

        app.MapGet("/v{version:apiVersion}/sample", () => "v1")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0));

        app.MapGet("/v{version:apiVersion}/sample", () => "v2")
            .WithApiVersionSet(versionSet)
            .HasDeprecatedApiVersion(new ApiVersion(2, 0));

        app.MapElRosoOpenApi();

        await app.StartAsync();
        return app;
    }

    private sealed class OnlyOpenApiControllers : ControllerFeatureProvider
    {
        protected override bool IsController(TypeInfo typeInfo) =>
            typeInfo.AsType() == typeof(OpenApiV1Controller) || typeInfo.AsType() == typeof(OpenApiV2Controller);
    }
}

[ApiController]
[Route("v1/ping")]
internal sealed class OpenApiV1Controller : ControllerBase
{
    [HttpGet]
    [ApiVersion(1.0)]
    public string Get() => "v1";
}

[ApiController]
[Route("v2/ping")]
internal sealed class OpenApiV2Controller : ControllerBase
{
    [HttpGet]
    [ApiVersion(2.0, Deprecated = true)]
    public string Get() => "v2";
}

