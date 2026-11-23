namespace ElRoso.Commons.Tests;

using ElRoso.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;

// Resolución de la dirección de quien llama detrás de un reverse proxy.
public class ForwardedHeadersSetupTests
{
    // Confía en un solo salto: lee la última entrada de X-Forwarded-For, la que escribió el proxy propio.
    [Fact]
    public void TrustsExactlyOneHopSoTheClientCannotForgeItsAddress()
    {
        var options = Resolve(configured: null);

        Assert.Equal(1, options.ForwardLimit);
    }

    [Fact]
    public void ReadsTheForwardedAddress()
    {
        var options = Resolve(configured: null);

        Assert.True(options.ForwardedHeaders.HasFlag(
            Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor));
    }

    // El default cubre loopback y rangos privados; el despliegue debe configurar sus redes.
    [Fact]
    public void ByDefaultTrustsLoopbackAndThePrivateRanges()
    {
        var options = Resolve(configured: null);

        Assert.Contains(options.KnownIPNetworks, n => n.Contains(IPAddress.Parse("127.0.0.1")));
        Assert.Contains(options.KnownIPNetworks, n => n.Contains(IPAddress.Parse("172.20.0.5")));
        Assert.Contains(options.KnownIPNetworks, n => n.Contains(IPAddress.Parse("10.1.2.3")));
        Assert.Contains(options.KnownIPNetworks, n => n.Contains(IPAddress.Parse("192.168.1.1")));
    }

    [Fact]
    public void AConfiguredListReplacesTheDefaults()
    {
        var options = Resolve("203.0.113.0/24");

        Assert.Contains(options.KnownIPNetworks, n => n.Contains(IPAddress.Parse("203.0.113.2")));
        Assert.DoesNotContain(options.KnownIPNetworks, n => n.Contains(IPAddress.Parse("10.1.2.3")));
    }

    // EN: 🔴 This used to assert the opposite — a malformed entry was skipped and the app booted.
    //     "Trusting one network less" is not what that costs here: the ONE network configured is the
    //     proxy, so dropping it means X-Forwarded-For is ignored and every caller looks like the
    //     proxy again. That is the 2026-08-03 outage, silently, from a single typo: no error, no
    //     log, /healthz green. A deployment value that cannot be read is a deployment error, and the
    //     only moment anybody is watching is the deploy itself.
    // ES: 🔴 Esto antes afirmaba lo contrario — una entrada mal escrita se salteaba y la app
    //     arrancaba. "Confiar en una red menos" no es lo que cuesta acá: la ÚNICA red configurada es
    //     la del proxy, así que descartarla significa ignorar X-Forwarded-For y que todos los que
    //     llaman vuelvan a parecer el proxy. Ésa es la caída del 2026-08-03, en silencio, por un solo
    //     dedazo: sin error, sin log, /healthz en verde. Un valor de despliegue que no se puede leer
    //     es un error de despliegue, y el único momento en que alguien está mirando es el deploy.
    [Theory]
    [InlineData("no-es-una-red")]
    [InlineData("203.0.113.0/33")]
    [InlineData("203.0.113.O/24")]
    [InlineData("999.999.999.999/8")]
    public void AMalformedEntryStopsTheAppInsteadOfSilentlyUnpartitioningIt(string malformed)
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(
            () => services.AddElRosoForwardedHeaders(BuildConfiguration($"{malformed}, 203.0.113.0/24")));
    }

    // EN: The message has to name the entry: the whole point is that whoever reads the failed deploy
    //     can see which value to fix without opening the code.
    // ES: El mensaje tiene que nombrar la entrada: la gracia es que quien lee el deploy fallido vea
    //     qué valor corregir sin abrir el código.
    [Fact]
    public void TheFailureNamesTheOffendingEntryAndTheSettingItCameFrom()
    {
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(
            () => services.AddElRosoForwardedHeaders(BuildConfiguration("203.0.113.0/33")));

        Assert.Contains("203.0.113.0/33", error.Message);
        Assert.Contains(ForwardedHeadersSetup.KnownNetworksKey, error.Message);
    }

    // EN: It fails while registering, before the host is built — not on the first request that
    //     happens to read an address. Loud at deploy time is the whole value of the change.
    // ES: Falla al registrar, antes de construir el host — no en el primer request que casualmente
    //     lea una dirección. Fallar fuerte en el deploy es todo el valor del cambio.
    [Fact]
    public void AValidListRegistersWithoutTouchingTheServiceProvider()
    {
        var services = new ServiceCollection();

        var returned = services.AddElRosoForwardedHeaders(BuildConfiguration("203.0.113.0/24, 10.0.0.0/8"));

        Assert.Same(services, returned);
    }

    // EN: Absent or blank still falls back to the defaults — that is deliberate and stays. Only an
    //     unreadable value is an error; "not configured" is a documented default.
    // ES: Ausente o en blanco sigue cayendo a los defaults — es deliberado y se queda. Solo un valor
    //     ilegible es un error; "sin configurar" es un default documentado.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyListFallsBackToTheDefaults(string configured)
    {
        var options = Resolve(configured);

        Assert.Contains(options.KnownIPNetworks, n => n.Contains(IPAddress.Parse("127.0.0.1")));
        Assert.Contains(options.KnownIPNetworks, n => n.Contains(IPAddress.Parse("10.1.2.3")));
    }

    // EN: End to end over a real HttpContext: this is the only thing that proves the forged entry
    //     loses to the one the proxy appended.
    // ES: De punta a punta sobre un HttpContext real: es lo único que prueba que la entrada
    //     falsificada pierde contra la que appendeó el proxy.
    [Fact]
    public async Task TheProxysEntryWinsOverTheOneTheClientInvented()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.2");
        context.Request.Headers["X-Forwarded-For"] = "1.2.3.4, 192.0.2.77";

        await ApplyMiddleware(context, "203.0.113.0/24");

        Assert.Equal("192.0.2.77", context.Connection.RemoteIpAddress?.ToString());
    }

    // EN: And an untrusted hop is ignored outright — otherwise anyone reaching the app directly
    //     could name themselves.
    // ES: Y un salto no confiable se ignora de plano — si no, cualquiera que llegue directo a la app
    //     podría auto-nombrarse.
    [Fact]
    public async Task AnUntrustedProxyIsNotBelieved()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.9");
        context.Request.Headers["X-Forwarded-For"] = "1.2.3.4";

        await ApplyMiddleware(context, "203.0.113.0/24");

        Assert.Equal("198.51.100.9", context.Connection.RemoteIpAddress?.ToString());
    }

    private static Microsoft.AspNetCore.Builder.ForwardedHeadersOptions Resolve(string? configured)
    {
        var services = new ServiceCollection();
        services.AddElRosoForwardedHeaders(BuildConfiguration(configured));

        return services.BuildServiceProvider()
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>>()
            .Value;
    }

    private static async Task ApplyMiddleware(HttpContext context, string? configured)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddElRosoForwardedHeaders(BuildConfiguration(configured));
        var provider = services.BuildServiceProvider();

        var builder = new ApplicationBuilder(provider);
        builder.UseElRosoForwardedHeaders();
        builder.Run(_ => Task.CompletedTask);

        await builder.Build().Invoke(context);
    }

    private static IConfiguration BuildConfiguration(string? configured)
    {
        var values = new Dictionary<string, string?>();
        if (configured is not null)
        {
            values["ForwardedHeaders:KnownNetworks"] = configured;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
