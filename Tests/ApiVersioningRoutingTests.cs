namespace ElRoso.Commons.Tests;

using System.Net;
using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using ElRoso.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class ApiVersioningRoutingTests
{
    private static async Task<IHost> StartAsync()
    {
        return await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services
                        .AddControllers()
                        .AddApplicationPart(typeof(VersionedPingController).Assembly)
                        .ConfigureApplicationPartManager(manager =>
                        {
                            // Deja solo el controller de este test para no mezclar versiones de otros.
                            var defaults = manager.FeatureProviders.OfType<ControllerFeatureProvider>().ToList();
                            defaults.ForEach(provider => manager.FeatureProviders.Remove(provider));
                            manager.FeatureProviders.Add(new OnlyVersionedPingController());
                        });
                    services.AddApiVersioningCustom(new ConfigurationBuilder().Build());
                });
                web.Configure(app =>
                {
                    app.UseRouting();
#pragma warning disable ASP0014
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
#pragma warning restore ASP0014
                });
            })
            .StartAsync();
    }

    [Theory]
    [InlineData("/v1/ping", "1.0")]
    [InlineData("/v2/ping", "2.0")]
    public async Task VersionedRoute_AnswersWithTheRequestedVersion(string path, string expected)
    {
        using var host = await StartAsync();

        var response = await host.GetTestClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadAsStringAsync());
        Assert.Equal("1.0, 2.0", string.Join(", ", response.Headers.GetValues("api-supported-versions")));
    }

    [Fact]
    public async Task VersionedRoute_WithAnUnknownVersion_ReturnsNotFound()
    {
        using var host = await StartAsync();

        var response = await host.GetTestClient().GetAsync("/v3/ping");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DescriptionProvider_ListsOneGroupPerVersion()
    {
        using var host = await StartAsync();

        var provider = host.Services.GetRequiredService<IApiVersionDescriptionProvider>();

        Assert.Equal(["v1", "v2"], provider.ApiVersionDescriptions.Select(d => d.GroupName).Order());
    }

    private sealed class OnlyVersionedPingController : ControllerFeatureProvider
    {
        protected override bool IsController(TypeInfo typeInfo) => typeInfo.AsType() == typeof(VersionedPingController);
    }
}

[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("v{version:apiVersion}/ping")]
internal sealed class VersionedPingController : ControllerBase
{
    [HttpGet]
    public string Get()
    {
        var version = this.HttpContext.Features.Get<IApiVersioningFeature>()?.RequestedApiVersion;
        return version is null ? string.Empty : $"{version.MajorVersion}.{version.MinorVersion ?? 0}";
    }
}
