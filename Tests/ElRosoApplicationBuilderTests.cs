namespace ElRoso.Commons.Tests;

using ElRoso.Commons;
using ElRoso.DependencyInjection;
using ElRoso.Domains.Session;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;

/// <summary>
/// EN: Integration tests over the real pipeline. These did not exist, and their absence is exactly
///     why nobody noticed that <c>UseElRoso</c> leaves no place to mount
///     <c>CurrentUserMiddleware</c>: it closes with UseEndpoints, so anything added afterwards never
///     runs, and anything added before runs while the caller is still unauthenticated. The symptom
///     is silent — CreatedBy and ModifiedBy just get written as null.
/// ES: Tests de integración sobre el pipeline real. No existían, y su ausencia es justo la razón por
///     la que nadie notó que <c>UseElRoso</c> no deja lugar donde montar
///     <c>CurrentUserMiddleware</c>: cierra con UseEndpoints, así que lo que se agrega después nunca
///     corre, y lo que se agrega antes corre cuando quien llama todavía no está autenticado. El
///     síntoma es silencioso — CreatedBy y ModifiedBy se graban en null y ya.
/// </summary>
public class ElRosoApplicationBuilderTests
{
    private const string ObjectIdentifierClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    private static readonly Guid TestUserId = Guid.NewGuid();

    // EN: Authenticates every request, so the pipeline reaches CurrentUserMiddleware with a real
    //     ClaimsPrincipal — which is the only state in which that middleware does anything.
    // ES: Autentica todo request, para que el pipeline llegue a CurrentUserMiddleware con un
    //     ClaimsPrincipal real — que es el único estado en el que ese middleware hace algo.
    private sealed class AlwaysAuthenticatedMiddleware(RequestDelegate next)
    {
        public Task InvokeAsync(HttpContext context)
        {
            var identity = new ClaimsIdentity(
                [
                    new Claim(ObjectIdentifierClaimType, TestUserId.ToString()),
                    new Claim(ClaimTypes.Surname, "Doe"),
                    new Claim(ClaimTypes.GivenName, "John"),
                    new Claim(ClaimTypes.Upn, "john.doe@elroso.ar"),
                ],
                authenticationType: "Test");

            context.User = new ClaimsPrincipal(identity);
            return next(context);
        }
    }

    private static async Task<IHost> StartHostAsync(Action<IApplicationBuilder> configure)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddControllers();   // UseEndpointsCustom maps them
                    services.AddCurrentUserMiddleware();
                });
                web.Configure(configure);
            })
            .StartAsync();

        return host;
    }

    // EN: The endpoint reports what the pipeline actually produced: the id of the current user, or
    //     "sin-usuario" when the audit trail would have been written as null.
    // ES: El endpoint reporta lo que el pipeline realmente produjo: el id del usuario actual, o
    //     "sin-usuario" cuando la auditoría se habría grabado en null.
    private static void MapProbe(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/quien-soy", (HttpContext ctx) =>
            (ctx.Items["currentUser"] as CurrentUser)?.Id.ToString() ?? "sin-usuario");

    [Fact]
    public async Task UseElRoso_WithoutTheInsertionPoint_LeavesTheAuditTrailEmpty()
    {
        // EN: Documents the trap, so nobody "fixes" it by removing the parameter later: with no
        //     insertion point there is no way to get a CurrentUser, and nothing fails.
        // ES: Documenta la trampa, para que nadie lo "arregle" sacando el parámetro más adelante:
        //     sin punto de inserción no hay forma de tener CurrentUser, y no falla nada.
        using var host = await StartHostAsync(app =>
        {
            app.UseMiddleware<AlwaysAuthenticatedMiddleware>();
            app.UseRouting();
            app.UseEndpoints(MapProbe);
        });

        var response = await host.GetTestClient().GetStringAsync("/quien-soy");

        Assert.Equal("sin-usuario", response);
    }

    [Fact]
    public async Task UseElRoso_MiddlewareAddedAfterTheEndpoints_NeverRuns()
    {
        // EN: The mechanical reason H-02 exists. UseEndpoints terminates the pipeline.
        // ES: La razón mecánica por la que existe H-02. UseEndpoints termina el pipeline.
        using var host = await StartHostAsync(app =>
        {
            app.UseMiddleware<AlwaysAuthenticatedMiddleware>();
            app.UseRouting();
            app.UseEndpoints(MapProbe);
            app.UseCurrentUserMiddleware();   // too late — this never executes
        });

        var response = await host.GetTestClient().GetStringAsync("/quien-soy");

        Assert.Equal("sin-usuario", response);
    }

    [Fact]
    public async Task UseElRoso_WithTheInsertionPoint_FillsTheCurrentUser()
    {
        // EN: The fix: a hook that runs after authentication and before the endpoints are mapped.
        // ES: El arreglo: un hook que corre después de la autenticación y antes de mapear endpoints.
        using var host = await StartHostAsync(app =>
        {
            app.UseMiddleware<AlwaysAuthenticatedMiddleware>();
            app.UseRouting();
            app.UseEndpointsCustom(
                new FakeEnvironment(),
                beforeEndpoints: inner => inner.UseCurrentUserMiddleware(),
                mapExtra: MapProbe);
        });

        var response = await host.GetTestClient().GetStringAsync("/quien-soy");

        Assert.Equal(TestUserId.ToString(), response);
    }

    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;

        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();

        public string ApplicationName { get; set; } = "Tests";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = "Development";
    }
}
