namespace ElRoso.Commons.Tests;

using ElRoso.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

public class ConfigureServicesTests
{
    private static IConfiguration BuildConfiguration(params string[] origins)
    {
        var settings = new Dictionary<string, string?>();
        for (var i = 0; i < origins.Length; i++)
        {
            settings[$"Cors:AllowedOrigins:{i}"] = origins[i];
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private static (IServiceCollection Services, Mock<ILogger> Logger) BuildServices(string environmentName)
    {
        var services = new ServiceCollection();
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns(environmentName);
        services.AddSingleton(environment.Object);

        var logger = new Mock<ILogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        return (services, logger);
    }

    private static void VerifyWarned(Mock<ILogger> logger, Times times) =>
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("CORS", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void AddCorsCustom_WithNoOriginsOutsideDevelopment_Warns(string environmentName)
    {
        // EN: With no Cors:AllowedOrigins the policy falls back to AllowAnyOrigin. That is fine in
        //     Development and it is a hole in production — and until now the fallback was completely
        //     silent, so an API could sit there open to any origin because of a missing key.
        // ES: Sin Cors:AllowedOrigins la política cae en AllowAnyOrigin. Está bien en Development y
        //     es un agujero en producción — y hasta ahora el fallback era completamente silencioso,
        //     así que una API podía quedar abierta a cualquier origen por una clave faltante.
        var (services, logger) = BuildServices(environmentName);

        services.AddCorsCustom(BuildConfiguration(), logger.Object);

        VerifyWarned(logger, Times.Once());
    }

    [Fact]
    public void AddCorsCustom_WithNoOriginsInDevelopment_DoesNotWarn()
    {
        // EN: In Development the open fallback is the intended convenience, not a finding.
        // ES: En Development el fallback abierto es la comodidad buscada, no un hallazgo.
        var (services, logger) = BuildServices("Development");

        services.AddCorsCustom(BuildConfiguration(), logger.Object);

        VerifyWarned(logger, Times.Never());
    }

    [Fact]
    public void AddCorsCustom_WithConfiguredOrigins_DoesNotWarn()
    {
        var (services, logger) = BuildServices("Production");

        services.AddCorsCustom(BuildConfiguration("https://mi-app.com"), logger.Object);

        VerifyWarned(logger, Times.Never());
    }

    [Fact]
    public void AddCorsCustom_WithoutALogger_StillRegistersThePolicy()
    {
        // EN: The logger is optional so the existing two-argument call keeps compiling.
        // ES: El logger es opcional para que la llamada de dos argumentos siga compilando.
        var services = new ServiceCollection();

        var exception = Record.Exception(() => services.AddCorsCustom(BuildConfiguration()));

        Assert.Null(exception);
    }

    // ── Registro de servicios ────────────────────────────────────────────────────────────────
    // EN: These extensions are the front door of the library: every consumer app runs them at
    //     startup, and none of them had a single test. A registration that silently stops
    //     happening is the kind of thing that only shows up in production.
    // ES: Estas extensiones son la puerta de entrada de la librería: toda app consumidora las
    //     corre al arrancar, y ninguna tenía un solo test. Un registro que deja de ocurrir en
    //     silencio es de esas cosas que aparecen recién en producción.

    private static IConfiguration ConfigFrom(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    [Fact]
    public void AddApiVersioningCustom_RegistersTheVersioningServices()
    {
        var services = new ServiceCollection();

        services.AddApiVersioningCustom(ConfigFrom(new() { ["DefaultApiVersion"] = "2.1" }));

        Assert.Contains(services, d => d.ServiceType.Name.Contains("ApiVersion", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-numero")]
    public void AddApiVersioningCustom_WithAnUnusableVersion_FallsBackToOne(string? version)
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(() =>
            services.AddApiVersioningCustom(ConfigFrom(new() { ["DefaultApiVersion"] = version })));

        Assert.Null(exception);
    }

    [Fact]
    public void AddAuthenticationJWT_WithoutASigningKey_FailsAtStartup()
    {
        // EN: The only auth setting whose absence is fatal, and it should be: without it every
        //     token would be accepted or rejected for the wrong reason.
        // ES: El único setting de auth cuya ausencia es fatal, y está bien que lo sea: sin él, todo
        //     token se aceptaría o rechazaría por el motivo equivocado.
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddAuthenticationJWT(ConfigFrom(new() { ["JWT:Issuer"] = "https://api" })));
    }

    [Fact]
    public void AddAuthenticationJWT_WithASigningKey_Registers()
    {
        var services = new ServiceCollection();

        services.AddAuthenticationJWT(ConfigFrom(new()
        {
            ["JWT:Issuer"] = "https://api.elroso.ar",
            ["JWT:Audience"] = "https://app.elroso.ar",
            ["JWT:IssuerSigningKey"] = "una-clave-larga-de-al-menos-32-caracteres!!",
        }));

        Assert.Contains(services, d => d.ServiceType == typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService));
    }

    [Fact]
    public void AddAuthenticationAzureAD_Registers()
    {
        var services = new ServiceCollection();

        services.AddAuthenticationAzureAD(ConfigFrom(new()
        {
            ["AzureAd:ClientId"] = "00000000-0000-0000-0000-000000000000",
            ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000001",
        }));

        Assert.Contains(services, d => d.ServiceType == typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService));
    }

    [Fact]
    public void AddControllersCustom_WithTheStringEnumConverter_Registers()
    {
        var services = new ServiceCollection();

        services.AddControllersCustom(ConfigFrom(new() { ["UseJsonStringEnumConverter"] = "true" }));

        Assert.Contains(services, d => d.ServiceType == typeof(Microsoft.Extensions.Options.IConfigureOptions<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>));
    }

    [Fact]
    public void AddEncryptionHelper_RegistersASingletonWithTheConfiguredWorkFactor()
    {
        var services = new ServiceCollection();
        services.AddEncryptionHelper(ConfigFrom(new() { ["Encryption:WorkFactor"] = "4" }));

        using var provider = services.BuildServiceProvider();
        var helper = provider.GetRequiredService<ElRoso.Helpers.EncryptionHelper>();

        Assert.Same(helper, provider.GetRequiredService<ElRoso.Helpers.EncryptionHelper>());
        Assert.StartsWith("$2", helper.HashPassword("secreto"), StringComparison.Ordinal);
    }

    [Fact]
    public void AddEncryptionHelper_WithAnInvalidWorkFactor_FailsWhenResolved()
    {
        var services = new ServiceCollection();
        services.AddEncryptionHelper(ConfigFrom(new() { ["Encryption:WorkFactor"] = "99" }));

        using var provider = services.BuildServiceProvider();

        Assert.Throws<ArgumentOutOfRangeException>(() => provider.GetRequiredService<ElRoso.Helpers.EncryptionHelper>());
    }

    [Fact]
    public void AddMongoDb_WithoutAConnectionString_FailsWhenResolved()
    {
        var services = new ServiceCollection();
        services.AddMongoDb(ConfigFrom(new()));

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<MongoDB.Driver.IMongoClient>());
    }

    [Fact]
    public void AddMongoDb_WithoutADatabaseName_FailsWhenResolved()
    {
        var services = new ServiceCollection();
        services.AddMongoDb(ConfigFrom(new() { ["ConnectionStrings:MongoDB"] = "mongodb://localhost:27017" }));

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<MongoDB.Driver.IMongoDatabase>());
    }

    [Fact]
    public void AddMongoDb_RegistersTheClientAsASingleton()
    {
        // EN: MongoClient owns the connection pool — one per repository would destroy pooling.
        // ES: MongoClient es dueño del pool de conexiones — uno por repositorio lo destruiría.
        var services = new ServiceCollection();
        services.AddMongoDb(ConfigFrom(new()
        {
            ["ConnectionStrings:MongoDB"] = "mongodb://localhost:27017",
            ["MongoDb:DatabaseName"] = "ElRosoDB",
        }));

        using var provider = services.BuildServiceProvider();

        Assert.Same(
            provider.GetRequiredService<MongoDB.Driver.IMongoClient>(),
            provider.GetRequiredService<MongoDB.Driver.IMongoClient>());
    }

    [Theory]
    [InlineData("AddS3BlobStorage")]
    [InlineData("AddAzureBlobStorage")]
    public void BlobStorage_RegistersASingleImplementationOfTheContract(string method)
    {
        // EN: The two are mutually exclusive: each registers IBlobStorageService, so calling both
        //     leaves the last one silently winning.
        // ES: Los dos son mutuamente excluyentes: cada uno registra IBlobStorageService, así que
        //     llamar a ambos deja ganando al último, en silencio.
        var services = new ServiceCollection();

        if (method == "AddS3BlobStorage")
        {
            services.AddS3BlobStorage();
        }
        else
        {
            services.AddAzureBlobStorage();
        }

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ElRoso.Services.IBlobStorageService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddSharePointFileService_RegistersASingleton()
    {
        var services = new ServiceCollection();

        services.AddSharePointFileService();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ElRoso.Services.ISharePointFileService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddMailService_RegistersASingleton()
    {
        var services = new ServiceCollection();

        services.AddMailService();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ElRoso.Services.IMailService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddMemoryCacheCustom_RegistersTheCache()
    {
        var services = new ServiceCollection();

        services.AddMemoryCacheCustom();

        Assert.Contains(services, d => d.ServiceType == typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache));
    }

    [Fact]
    public void AddCurrentUserMiddleware_AndExceptionHandler_RegisterAsTransient()
    {
        var services = new ServiceCollection();

        services.AddCurrentUserMiddleware();
        services.AddExceptionHandlerMiddleware();

        Assert.All(
            services,
            d => Assert.Equal(ServiceLifetime.Transient, d.Lifetime));
    }
}
