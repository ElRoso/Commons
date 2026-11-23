namespace ElRoso.Commons.Tests;

using System.Net;
using System.Security.Claims;
using System.Text.Json;
using ElRoso.DependencyInjection;
using ElRoso.Domains.Session;
using ElRoso.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Un Host armado solo con las extensiones AddX/UseX sueltas se comporta como el de UseElRoso.
public class ComposableBootstrapTests
{
    private const string ObjectIdentifierClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    private static readonly Guid TestUserId = Guid.NewGuid();

    [Fact]
    public async Task ATypedExceptionBecomesAProblemResponseWithItsStatus()
    {
        await using var app = await StartAsync(healthy: true);

        var response = await app.GetTestClient().GetAsync("/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("recurso inexistente", body.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task TheCurrentUserIsFilledAfterAuthentication()
    {
        await using var app = await StartAsync(healthy: true);

        var response = await app.GetTestClient().GetStringAsync("/quien-soy");

        Assert.Equal(TestUserId.ToString(), response);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.ServiceUnavailable)]
    public async Task TheHealthCheckAnswersWithoutPublishingTheExceptionMessage(bool healthy, HttpStatusCode expected)
    {
        await using var app = await StartAsync(healthy);

        var response = await app.GetTestClient().GetAsync("/healthz");

        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain("clave-secreta", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheForwardedAddressIsTakenFromTheLastHop()
    {
        await using var app = await StartAsync(healthy: true);

        var context = await app.GetTestServer().SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Path = "/ip";
            c.Connection.RemoteIpAddress = IPAddress.Loopback;
            c.Request.Headers["X-Forwarded-For"] = "1.2.3.4, 192.0.2.77";
        });

        using var reader = new StreamReader(context.Response.Body);
        Assert.Equal("192.0.2.77", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task TheInfoEndpointAnswersAtTheRoot()
    {
        await using var app = await StartAsync(healthy: true);

        var response = await app.GetTestClient().GetStringAsync("/");

        Assert.Contains(Environments.Production, response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLoggerSilencesTheFrameworkRequestLoggerBelowWarning()
    {
        await using var app = await StartAsync(healthy: true);

        var factory = app.Services.GetRequiredService<ILoggerFactory>();
        var diagnostics = factory.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");

        Assert.False(diagnostics.IsEnabled(LogLevel.Information));
        Assert.True(diagnostics.IsEnabled(LogLevel.Warning));
    }

    [Fact]
    public void TheConfigurationSourcesKeepTheOrderOfCreateBuilder()
    {
        var composed = WebApplication.CreateBuilder(Options());
        composed.AddElRosoConfiguration([]);

#pragma warning disable CS0618 // se compara contra el bootstrap de compatibilidad a propósito
        var legacy = ElRosoApplicationBuilder.CreateBuilder([]);
#pragma warning restore CS0618

        Assert.Equal(
            legacy.Configuration.Sources.Select(s => s.GetType()),
            composed.Configuration.Sources.Select(s => s.GetType()));
    }

    private static WebApplicationOptions Options() => new()
    {
        EnvironmentName = Environments.Production,
        ApplicationName = typeof(ComposableBootstrapTests).Assembly.GetName().Name,
    };

    // Arma el Host solo con las extensiones públicas, sin CreateBuilder ni UseElRoso.
    private static async Task<WebApplication> StartAsync(bool healthy)
    {
        var builder = WebApplication.CreateBuilder(Options());
        builder.AddElRosoConfiguration([$"--Logging:FilePath={Path.Combine(Path.GetTempPath(), "elroso-commons-tests")}"]);
        builder.AddElRosoSerilog();
        builder.WebHost.UseTestServer();

        builder.Services.AddElRosoForwardedHeaders(builder.Configuration);
        builder.Services.AddExceptionHandlerMiddleware();
        builder.Services.AddCurrentUserMiddleware();
        builder.Services.AddControllersCustom(builder.Configuration);
        builder.Services.AddHealthChecks().AddCheck("base-de-datos", () =>
            healthy ? HealthCheckResult.Healthy() : throw new InvalidOperationException("clave-secreta"));

        var app = builder.Build();

        app.UseElRosoForwardedHeaders();
        app.UseExceptionHandlerMiddleware();
        app.UseRouting();
        app.Use((context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ObjectIdentifierClaimType, TestUserId.ToString()),
                    new Claim(ClaimTypes.Surname, "Doe"),
                    new Claim(ClaimTypes.GivenName, "John"),
                    new Claim(ClaimTypes.Upn, "john.doe@example.com"),
                ],
                authenticationType: "Test"));
            return next(context);
        });
        app.UseCurrentUserMiddleware();

        app.MapControllers();
        app.MapHealthChecksCustom();
        app.MapElRosoInfo(app.Environment);
        app.MapGet("/no-existe", (Func<string>)(() => throw new NotFoundException("recurso inexistente")));
        app.MapGet("/quien-soy", (HttpContext ctx) => (ctx.Items["currentUser"] as CurrentUser)?.Id.ToString() ?? "sin-usuario");
        app.MapGet("/ip", (HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "sin-ip");

        await app.StartAsync();
        return app;
    }
}
