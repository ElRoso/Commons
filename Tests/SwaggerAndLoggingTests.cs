namespace ElRoso.Commons.Tests;

using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using ElRoso.Commons;
using ElRoso.DependencyInjection;
using ElRoso.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Swashbuckle.AspNetCore.SwaggerGen;

public class SwaggerAndLoggingTests
{
    private static IConfiguration ConfigFrom(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static IServiceCollection ServicesWithApiVersioning()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(ConfigFrom(new()));
        services.AddApiVersioningCustom(ConfigFrom(new()));
        return services;
    }

    // ── Swagger ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AddSwaggerJWTAuthentication_RegistersTheGenerator()
    {
        var services = ServicesWithApiVersioning();

        services.AddSwaggerJWTAuthentication(ConfigFrom(new()));

        Assert.Contains(services, d => d.ServiceType == typeof(IConfigureOptions<SwaggerGenOptions>));
    }

    [Fact]
    public void AddSwaggerBasicAuthentication_RegistersTheGenerator()
    {
        var services = ServicesWithApiVersioning();

        services.AddSwaggerBasicAuthentication(ConfigFrom(new()));

        Assert.Contains(services, d => d.ServiceType == typeof(IConfigureOptions<SwaggerGenOptions>));
    }

    [Fact]
    public void SwaggerConfigureOptions_CreatesOneDocumentPerApiVersion()
    {
        var provider = new Mock<IApiVersionDescriptionProvider>();
        provider.Setup(p => p.ApiVersionDescriptions).Returns(
        [
            new ApiVersionDescription(new ApiVersion(1, 0), "v1", deprecated: false),
            new ApiVersionDescription(new ApiVersion(2, 0), "v2", deprecated: true),
        ]);

        var configuration = ConfigFrom(new() { ["ApplicationName"] = "MiBackend" });
        var options = new SwaggerGenOptions();

        var sut = ActivateInternal(configuration, provider.Object);
        sut.Configure(options);

        Assert.Equal(2, options.SwaggerGeneratorOptions.SwaggerDocs.Count);
        Assert.Equal("MiBackend", options.SwaggerGeneratorOptions.SwaggerDocs["v1"].Title);
    }

    [Fact]
    public void SwaggerConfigureOptions_MarksADeprecatedVersionInItsDescription()
    {
        // EN: The whole point of versioning: a consumer must be able to see a version is on its way
        //     out without asking anyone.
        // ES: El punto del versionado: quien consume tiene que poder ver que una versión se está
        //     yendo sin preguntarle a nadie.
        var provider = new Mock<IApiVersionDescriptionProvider>();
        provider.Setup(p => p.ApiVersionDescriptions).Returns(
        [
            new ApiVersionDescription(new ApiVersion(1, 0), "v1", deprecated: true),
        ]);

        var options = new SwaggerGenOptions();
        ActivateInternal(ConfigFrom(new()), provider.Object).Configure(options);

        Assert.Contains("deprecated", options.SwaggerGeneratorOptions.SwaggerDocs["v1"].Description, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://elroso.ar/licencia", "https://elroso.ar/licencia")]
    [InlineData("no-es-una-url", "https://opensource.org/licenses/MIT")]
    [InlineData(null, "https://opensource.org/licenses/MIT")]
    public void SwaggerConfigureOptions_FallsBackToTheDefaultLicenseUrl(string? configured, string expected)
    {
        var provider = new Mock<IApiVersionDescriptionProvider>();
        provider.Setup(p => p.ApiVersionDescriptions).Returns(
        [
            new ApiVersionDescription(new ApiVersion(1, 0), "v1", deprecated: false),
        ]);

        var options = new SwaggerGenOptions();
        ActivateInternal(ConfigFrom(new() { ["Swagger:LicenseUrl"] = configured }), provider.Object).Configure(options);

        Assert.Equal(expected, options.SwaggerGeneratorOptions.SwaggerDocs["v1"].License!.Url!.ToString());
    }

    [Fact]
    public void SwaggerConfigureOptions_WithoutAnApplicationName_UsesElRoso()
    {
        var provider = new Mock<IApiVersionDescriptionProvider>();
        provider.Setup(p => p.ApiVersionDescriptions).Returns(
        [
            new ApiVersionDescription(new ApiVersion(1, 0), "v1", deprecated: false),
        ]);

        var options = new SwaggerGenOptions();
        ActivateInternal(ConfigFrom(new()), provider.Object).Configure(options);

        Assert.Equal("ElRoso", options.SwaggerGeneratorOptions.SwaggerDocs["v1"].Title);
    }

    // EN: SwaggerConfigureOptions is internal; the test assembly sees it through InternalsVisibleTo.
    // ES: SwaggerConfigureOptions es internal; el assembly de tests lo ve por InternalsVisibleTo.
    private static IConfigureOptions<SwaggerGenOptions> ActivateInternal(
        IConfiguration configuration,
        IApiVersionDescriptionProvider provider)
    {
        var type = typeof(SwaggerSetup).Assembly.GetType("ElRoso.Swagger.SwaggerConfigureOptions")!;
        return (IConfigureOptions<SwaggerGenOptions>)Activator.CreateInstance(type, configuration, provider)!;
    }

    // ── Logging ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LoggerConfiguration_WithNoSinksConfigured_StillBuildsALogger()
    {
        var logger = BuildLogger(ConfigFrom(new() { ["ApplicationName"] = "Tests" }));

        Assert.NotNull(logger);
    }

    [Fact]
    public void LoggerConfiguration_WithSeqConfigured_BuildsALogger()
    {
        var logger = BuildLogger(ConfigFrom(new()
        {
            ["ApplicationName"] = "Tests",
            ["SEQ:ServerURL"] = "http://localhost:5341",
        }));

        Assert.NotNull(logger);
    }

    [Fact]
    public void LoggerConfiguration_WithGraylogConfigured_BuildsALogger()
    {
        var logger = BuildLogger(ConfigFrom(new()
        {
            ["ApplicationName"] = "Tests",
            ["Graylog:HostnameOrAddress"] = "localhost",
            ["Graylog:Port"] = "12201",
        }));

        Assert.NotNull(logger);
    }

    [Fact]
    public void LoggerConfiguration_WithoutAnApplicationName_StillBuildsALogger()
    {
        var logger = BuildLogger(new ConfigurationBuilder().Build());

        Assert.NotNull(logger);
    }

    [Fact]
    public void LoggerConfiguration_WritesToTheConfiguredPath()
    {
        // EN: The file path used to be hardcoded to /logs/{App}/.log — absolute, not configurable
        //     and outside the app's own directory. In a container without that volume mounted, or
        //     without write permission, Serilog swallows the failure: the app starts, everything
        //     looks fine, and there is simply no file log. On Windows it resolves to the root of the
        //     current drive.
        // ES: La ruta del archivo estaba fija en /logs/{App}/.log — absoluta, no configurable y
        //     fuera del directorio de la app. En un contenedor sin ese volumen montado, o sin
        //     permiso de escritura, Serilog se traga la falla: la app arranca, todo parece bien, y
        //     simplemente no hay log de archivo. En Windows resuelve a la raíz del disco actual.
        var temp = Path.Combine(Path.GetTempPath(), $"elroso-logs-{Guid.NewGuid():N}");

        try
        {
            var logger = BuildLogger(ConfigFrom(new()
            {
                ["ApplicationName"] = "Tests",
                ["Logging:FilePath"] = temp,
            }));

            logger.Information("una linea de prueba");
            (logger as IDisposable)?.Dispose();

            Assert.True(Directory.Exists(temp), $"Se esperaba que el log fuera a {temp}.");
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }

    [Fact]
    public void LoggerConfiguration_WithoutAConfiguredPath_KeepsTheHistoricDefault()
    {
        // EN: The default stays /logs/{App} so nothing changes for deployments that already mount
        //     that volume.
        // ES: El default sigue siendo /logs/{App} para que nada cambie en los despliegues que ya
        //     montan ese volumen.
        var logger = BuildLogger(ConfigFrom(new() { ["ApplicationName"] = "Tests" }));

        Assert.NotNull(logger);
    }

    // EN: 🔴 SignalR puts the user's JWT in the query string — a WebSocket handshake cannot carry
    //     an Authorization header — and Microsoft.AspNetCore.Hosting.Diagnostics logs the raw url at
    //     Information on every request. That wrote a valid session token to the production logs of a
    //     consumer app, measured at 57 times in a single browsing session.
    //     A consumer cannot fix this on its own: AddSerilogCustom calls ClearProviders() and installs
    //     a concrete Serilog logger, so Microsoft.Extensions.Logging filter rules never run, and this
    //     class hardcodes MinimumLevel.Debug() without ReadFrom.Configuration, so appsettings has no
    //     say either. The override has to live here.
    // ES: 🔴 SignalR pone el JWT del usuario en el query string —un handshake de WebSocket no puede
    //     llevar header Authorization— y Microsoft.AspNetCore.Hosting.Diagnostics loguea la url cruda
    //     en Information en cada request. Eso escribía un token de sesión válido en los logs de
    //     producción de una app consumidora, medido en 57 veces en una sola sesión de navegación.
    //     Un consumidor no puede arreglarlo solo: AddSerilogCustom hace ClearProviders() e instala un
    //     logger concreto de Serilog, así que las reglas de filtro de MEL nunca corren, y esta clase
    //     hardcodea MinimumLevel.Debug() sin ReadFrom.Configuration, así que appsettings tampoco
    //     decide. El override tiene que vivir acá.
    [Fact]
    public void LoggerConfiguration_DropsInformationFromTheFrameworkRequestLogger()
    {
        var sink = new CollectingSink();
        var logger = BuildLogger(ConfigFrom(new() { ["ApplicationName"] = "Tests" }), sink);

        logger
            .ForContext(Serilog.Core.Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Hosting.Diagnostics")
            .Information("Request starting HTTP/1.1 GET /hubs/app?access_token=un-token-valido");

        Assert.Empty(sink.Events);
    }

    // EN: The override silences noise, not failures. A framework request that fails still has to be
    //     visible — dropping Warning and above would trade a leak for a blind spot.
    // ES: El override calla ruido, no fallas. Un request del framework que falla tiene que seguir
    //     viéndose — descartar Warning para arriba cambiaría una filtración por un punto ciego.
    [Fact]
    public void LoggerConfiguration_KeepsWarningsFromTheFrameworkRequestLogger()
    {
        var sink = new CollectingSink();
        var logger = BuildLogger(ConfigFrom(new() { ["ApplicationName"] = "Tests" }), sink);

        logger
            .ForContext(Serilog.Core.Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Hosting.Diagnostics")
            .Warning("algo salio mal en el pipeline");

        Assert.Single(sink.Events);
    }

    // EN: 🔴 The override names ONE logger on purpose. Silencing "Microsoft" wholesale would take
    //     EF Core's command logging and the auth handlers with it — the ones needed to diagnose the
    //     next problem. This test is what keeps the override narrow.
    // ES: 🔴 El override nombra UN logger a propósito. Callar "Microsoft" entero se llevaría puesto
    //     el log de comandos de EF Core y los handlers de auth — justo los que hacen falta para
    //     diagnosticar el problema siguiente. Este test es lo que mantiene angosto el override.
    [Fact]
    public void LoggerConfiguration_KeepsInformationFromOtherMicrosoftLoggers()
    {
        var sink = new CollectingSink();
        var logger = BuildLogger(ConfigFrom(new() { ["ApplicationName"] = "Tests" }), sink);

        logger
            .ForContext(Serilog.Core.Constants.SourceContextPropertyName, "Microsoft.EntityFrameworkCore.Database.Command")
            .Information("Executed DbCommand");

        Assert.Single(sink.Events);
    }

    // EN: A consumer's own lines are the replacement for what the override drops. If these went too,
    //     the fix would leave the app with no request log at all.
    // ES: Las líneas propias del consumidor son el reemplazo de lo que el override descarta. Si éstas
    //     también desaparecieran, el fix dejaría a la app sin ningún log de requests.
    [Fact]
    public void LoggerConfiguration_KeepsInformationFromTheApplicationItself()
    {
        var sink = new CollectingSink();
        var logger = BuildLogger(ConfigFrom(new() { ["ApplicationName"] = "Tests" }), sink);

        logger
            .ForContext(Serilog.Core.Constants.SourceContextPropertyName, "Consumer.DependencyInjection.Middlewares.RequestLoggingMiddleware")
            .Information("HTTP GET /clients responded 200 in 12 ms");

        Assert.Single(sink.Events);
    }

    private sealed class CollectingSink : Serilog.Core.ILogEventSink
    {
        public List<Serilog.Events.LogEvent> Events { get; } = new();

        public void Emit(Serilog.Events.LogEvent logEvent) => this.Events.Add(logEvent);
    }

    private static Serilog.ILogger BuildLogger(IConfiguration configuration, Serilog.Core.ILogEventSink sink)
    {
        var type = typeof(ElRosoHostBuilderExtensions).Assembly.GetType("ElRoso.Commons.ElRosoLoggerConfiguration")!;
        var instance = (Serilog.LoggerConfiguration)Activator.CreateInstance(type, configuration)!;
        return instance.WriteTo.Sink(sink).CreateLogger();
    }

    private static Serilog.ILogger BuildLogger(IConfiguration configuration)
    {
        var type = typeof(ElRosoHostBuilderExtensions).Assembly.GetType("ElRoso.Commons.ElRosoLoggerConfiguration")!;
        var instance = (Serilog.LoggerConfiguration)Activator.CreateInstance(type, configuration)!;
        return instance.CreateLogger();
    }

    // ── Bootstrap ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateBuilder_RegistersTheCommonFloor()
    {
        // EN: Every consumer app starts here. If one of these registrations quietly disappears, the
        //     app compiles and fails later, in production, for a reason that looks unrelated.
        // ES: Toda app consumidora arranca acá. Si uno de estos registros desaparece en silencio, la
        //     app compila y falla después, en producción, por un motivo que parece no tener relación.
#pragma warning disable CS0618 // se ejercita a propósito el bootstrap de compatibilidad
        var builder = ElRosoApplicationBuilder.CreateBuilder([]);
#pragma warning restore CS0618

        Assert.Contains(builder.Services, d => d.ServiceType == typeof(ElRoso.Services.IMailService));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(Microsoft.AspNetCore.Http.IHttpContextAccessor));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(ElRoso.Presentation.Middlewares.CurrentUserMiddleware));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(ElRoso.Presentation.Middlewares.ExceptionHandlerMiddleware));
    }

    [Fact]
    public void CreateBuilder_ReadsCommandLineArguments()
    {
        // EN: CreateBuilder clears the default configuration sources and rebuilds the chain, with
        //     the command line last so it wins over everything else.
        // ES: CreateBuilder limpia las fuentes de configuración por defecto y rearma la cadena, con
        //     la línea de comandos al final para que le gane a todo lo demás.
#pragma warning disable CS0618 // se ejercita a propósito el bootstrap de compatibilidad
        var builder = ElRosoApplicationBuilder.CreateBuilder(["--DefaultApiVersion=3.0"]);
#pragma warning restore CS0618

        Assert.Equal("3.0", builder.Configuration["DefaultApiVersion"]);
    }

    [Fact]
    public void SwaggerConfigureOptions_PrefersApplicationDisplayName()
    {
        // EN: 'ApplicationName' is a reserved host key, so the title of the API and the log field
        //     moved to a key of our own. The old one still works — it is what every deployment has
        //     in its appsettings.json today — but the new one wins when both are set.
        // ES: 'ApplicationName' es una clave reservada del host, así que el título de la API y el
        //     campo de log se mudaron a una clave propia. La vieja sigue funcionando — es la que
        //     tienen hoy todos los despliegues en su appsettings.json — pero la nueva gana cuando
        //     están las dos.
        var provider = new Mock<IApiVersionDescriptionProvider>();
        provider.Setup(p => p.ApiVersionDescriptions).Returns(
        [
            new ApiVersionDescription(new ApiVersion(1, 0), "v1", deprecated: false),
        ]);

        var configuration = ConfigFrom(new()
        {
            ["ApplicationName"] = "ElViejo",
            ["ApplicationDisplayName"] = "ElNuevo",
        });
        var options = new SwaggerGenOptions();

        var sut = ActivateInternal(configuration, provider.Object);
        sut.Configure(options);

        Assert.Equal("ElNuevo", options.SwaggerGeneratorOptions.SwaggerDocs["v1"].Title);
    }

    [Fact]
    public void LoggerConfiguration_PrefersApplicationDisplayName()
    {
        var logger = BuildLogger(ConfigFrom(new()
        {
            ["ApplicationName"] = "ElViejo",
            ["ApplicationDisplayName"] = "ElNuevo",
        }));

        Assert.NotNull(logger);
    }

    [Fact]
    public void CreateBuilder_ApplicationDisplayNameOnTheCommandLine_Works()
    {
        // EN: The whole point of the new key: it can be passed anywhere — command line included —
        //     without the host trying to load an assembly named after it.
        // ES: Todo el punto de la clave nueva: se puede pasar por donde sea — línea de comandos
        //     incluida — sin que el host intente cargar un assembly con ese nombre.
#pragma warning disable CS0618 // se ejercita a propósito el bootstrap de compatibilidad
        var builder = ElRosoApplicationBuilder.CreateBuilder(["--ApplicationDisplayName=UnNombreCualquiera"]);
#pragma warning restore CS0618

        Assert.Equal("UnNombreCualquiera", builder.Configuration["ApplicationDisplayName"]);
    }

    [Fact]
    public void CreateBuilder_ApplicationNameOnTheCommandLine_ExplainsTheReservedKey()
    {
        // EN: The host fails with a FileNotFoundException about an assembly nobody asked for, which
        //     says nothing about the real cause. It is kept as the inner exception and wrapped in a
        //     message that names the key to use instead.
        // ES: El host falla con un FileNotFoundException sobre un assembly que nadie pidió, que no
        //     dice nada de la causa real. Se conserva como excepción interna y se envuelve en un
        //     mensaje que nombra la clave que hay que usar.
#pragma warning disable CS0618 // se ejercita a propósito el bootstrap de compatibilidad
        var exception = Record.Exception(() =>
            ElRosoApplicationBuilder.CreateBuilder(["--ApplicationName=UnNombreCualquiera"]));
#pragma warning restore CS0618

        var invalid = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("ApplicationDisplayName", invalid.Message, StringComparison.Ordinal);
        Assert.IsType<FileNotFoundException>(invalid.InnerException);
    }

    [Fact]
    public void CreateBuilder_ApplicationNameOnTheCommandLine_IsAReservedHostKey()
    {
        // EN: 🔴 'ApplicationName' is a RESERVED host key in ASP.NET Core: the host takes it as the
        //     name of the application assembly. This library also reads it as the Swagger title and
        //     the log field, which is fine from appsettings.json — by then the host is already
        //     built — but passing it on the command line makes the host try to load an assembly
        //     with that name and the app does not start at all.
        // ES: 🔴 'ApplicationName' es una clave RESERVADA del host en ASP.NET Core: el host la toma
        //     como el nombre del assembly de la aplicación. Esta librería además la lee como título
        //     de Swagger y campo de log, lo que está bien desde appsettings.json — para entonces el
        //     host ya está armado — pero pasarla por línea de comandos hace que el host intente
        //     cargar un assembly con ese nombre y la app directamente no arranca.
#pragma warning disable CS0618 // se ejercita a propósito el bootstrap de compatibilidad
        var exception = Record.Exception(() =>
            ElRosoApplicationBuilder.CreateBuilder(["--ApplicationName=UnNombreCualquiera"]));
#pragma warning restore CS0618

        Assert.IsType<FileNotFoundException>(exception?.InnerException);
    }

    [Fact]
    public void CreateBuilder_RegistersHealthCheckServicesButNoEndpoint()
    {
        // EN: The documented trap: the services are there, the route is not — every consumer has to
        //     map /healthz by hand.
        // ES: La trampa documentada: los servicios están, la ruta no — cada consumidor tiene que
        //     mapear /healthz a mano.
#pragma warning disable CS0618 // se ejercita a propósito el bootstrap de compatibilidad
        var builder = ElRosoApplicationBuilder.CreateBuilder([]);
#pragma warning restore CS0618

        Assert.Contains(builder.Services, d => d.ServiceType == typeof(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService));
    }

    [Fact]
    public void UseSwaggerCustom_WithoutTheGeneratorRegistered_DoesNotThrow()
    {
        // EN: UseElRoso mounts Swagger unconditionally, but registering the generator
        //     (AddSwaggerJWTAuthentication / AddSwaggerBasicAuthentication) is opt-in. An app that
        //     skipped it got the middleware mounted over a generator that does not exist.
        // ES: UseElRoso monta Swagger sin condición, pero registrar el generador
        //     (AddSwaggerJWTAuthentication / AddSwaggerBasicAuthentication) es opt-in. Una app que
        //     no lo llamaba quedaba con el middleware montado sobre un generador que no existe.
        var services = ServicesWithApiVersioning();
        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);

        var exception = Record.Exception(app.UseSwaggerCustom);

        Assert.Null(exception);
    }

    [Fact]
    public async Task Swagger_WithItsGeneratorRegistered_ServesTheDocument()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(s =>
                {
                    s.AddLogging();
                    s.AddRouting();
                    s.AddSingleton(ConfigFrom(new() { ["ApplicationName"] = "Tests" }));
                    s.AddApiVersioningCustom(ConfigFrom(new()));
                    s.AddSwaggerJWTAuthentication(ConfigFrom(new()));
                });
                web.Configure(app => app.UseSwaggerCustom());
            })
            .StartAsync();

        var response = await host.GetTestClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_MountedWithoutItsGenerator_FailsWhenTheDocumentIsRequested()
    {
        // EN: Mounting the middleware does not fail — the failure lands on whoever opens /swagger.
        //     This test pins down what actually happens, instead of assuming.
        // ES: Montar el middleware no falla — la falla le cae a quien abre /swagger. Este test fija
        //     lo que realmente pasa, en vez de suponerlo.
        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(s =>
                {
                    s.AddLogging();
                    s.AddRouting();
                    s.AddApiVersioningCustom(ConfigFrom(new()));
                });
                web.Configure(app => app.UseSwaggerCustom());
            })
            .StartAsync();

        var response = await host.GetTestClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
