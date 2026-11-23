namespace ElRoso.Commons.Tests;

using ElRoso.DependencyInjection;
using ElRoso.Domains.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;

public class HealthCheckSetupTests
{
    private static async Task<IHost> StartAsync(Action<IHealthChecksBuilder>? checks = null, bool mapAsEndpoint = true)
    {
        return await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    var builder = services.AddHealthChecks();
                    checks?.Invoke(builder);
                });
                web.Configure(app =>
                {
                    if (mapAsEndpoint)
                    {
                        app.UseRouting();
#pragma warning disable ASP0014
                        app.UseEndpoints(e => e.MapHealthChecksCustom());
#pragma warning restore ASP0014
                    }
                    else
                    {
                        app.UseHealthChecksCustom();
                    }
                });
            })
            .StartAsync();
    }

    private static async Task<HealthCheckResponse> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<HealthCheckResponse>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public async Task Healthz_WithNoChecksRegistered_IsHealthy()
    {
        // EN: An app with no checks of its own still has to answer that it is alive — that is what
        //     the orchestrator asks.
        // ES: Una app sin chequeos propios igual tiene que contestar que está viva — eso es lo que
        //     pregunta el orquestador.
        using var host = await StartAsync();

        var response = await host.GetTestClient().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", (await ReadAsync(response)).Status);
    }

    [Fact]
    public async Task Healthz_AnswersWithTheLibrarysOwnShape()
    {
        // EN: HealthCheckResponse and IndividualHealthCheckResponse existed in the library with no
        //     producer at all. This is what finally fills them.
        // ES: HealthCheckResponse e IndividualHealthCheckResponse existían en la librería sin ningún
        //     productor. Esto es lo que por fin los llena.
        using var host = await StartAsync(checks =>
            checks.AddCheck("base-de-datos", () => HealthCheckResult.Healthy("responde"))
                  .AddCheck("almacenamiento", () => HealthCheckResult.Healthy()));

        var payload = await ReadAsync(await host.GetTestClient().GetAsync("/healthz"));

        Assert.Equal(2, payload.HealthChecks.Count());
        Assert.Contains(payload.HealthChecks, c => c.Component == "base-de-datos" && c.Description == "responde");
        Assert.True(payload.HealthCheckDuration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task Healthz_WhenACheckIsUnhealthy_Answers503()
    {
        // EN: 503 is the part an orchestrator actually reads. Answering 200 with a body saying
        //     "Unhealthy" keeps a dead instance in the load balancer.
        // ES: El 503 es la parte que el orquestador realmente lee. Contestar 200 con un cuerpo que
        //     dice "Unhealthy" deja una instancia muerta en el balanceador.
        using var host = await StartAsync(checks =>
            checks.AddCheck("base-de-datos", () => HealthCheckResult.Unhealthy("no responde")));

        var response = await host.GetTestClient().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", (await ReadAsync(response)).Status);
    }

    [Fact]
    public async Task Healthz_WhenACheckThrows_DoesNotLeakTheException()
    {
        // EN: This endpoint is usually reachable without authentication, so an exception message —
        //     which can carry a connection string or a stack trace — must not travel in the body.
        // ES: Este endpoint suele ser accesible sin autenticar, así que el mensaje de una excepción
        //     — que puede traer un connection string o un stack trace — no debe viajar en el cuerpo.
        using var host = await StartAsync(checks =>
            checks.AddCheck("base-de-datos", () => throw new InvalidOperationException("Server=10.0.0.5;Password=secreto")));

        var body = await (await host.GetTestClient().GetAsync("/healthz")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("secreto", body, StringComparison.Ordinal);
        Assert.DoesNotContain("10.0.0.5", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseHealthChecksCustom_WorksWithoutRouting()
    {
        using var host = await StartAsync(mapAsEndpoint: false);

        var response = await host.GetTestClient().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MapHealthChecksCustom_AcceptsACustomRoute()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(s => { s.AddRouting(); s.AddHealthChecks(); });
                web.Configure(app =>
                {
                    app.UseRouting();
#pragma warning disable ASP0014
                    app.UseEndpoints(e => e.MapHealthChecksCustom("/estoy-vivo"));
#pragma warning restore ASP0014
                });
            })
            .StartAsync();

        Assert.Equal(HttpStatusCode.OK, (await host.GetTestClient().GetAsync("/estoy-vivo")).StatusCode);
    }
}
