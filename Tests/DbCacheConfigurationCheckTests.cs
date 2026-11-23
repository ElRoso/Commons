namespace ElRoso.Commons.Tests;

using ElRoso.DependencyInjection;
using ElRoso.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// EN: The cache lives in the process. With two replicas each one caches on its own and a write only
///     invalidates the replica that served it, so the other keeps answering with data up to 20
///     minutes old — the absolute expiration. Nothing in the code can detect how many replicas are
///     running, so the deployment has to declare it, and the check turns that declaration into a log
///     line at startup: silence was the whole problem.
/// ES: La cache vive en el proceso. Con dos réplicas cada una cachea por su cuenta y una escritura
///     solo invalida la que la atendió, así que la otra sigue respondiendo con datos de hasta 20
///     minutos — la expiración absoluta. Nada en el código puede detectar cuántas réplicas corren,
///     así que el despliegue lo declara, y el chequeo convierte esa declaración en una línea de log
///     al arrancar: el silencio era todo el problema.
/// </summary>
public class DbCacheConfigurationCheckTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    private static async Task<Mock<ILogger<DbCacheConfigurationCheck>>> RunCheckAsync(IConfiguration configuration)
    {
        var logger = new Mock<ILogger<DbCacheConfigurationCheck>>();
        var check = new DbCacheConfigurationCheck(configuration, logger.Object);

        await check.StartAsync(CancellationToken.None);

        return logger;
    }

    private static void VerifyLogged(Mock<ILogger<DbCacheConfigurationCheck>> logger, LogLevel level, Times times) =>
        logger.Verify(
            l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    [Fact]
    public async Task WithTheCacheOnAndSeveralReplicas_Warns()
    {
        var logger = await RunCheckAsync(Config(("DbCacheIsEnabled", "true"), ("DbCacheReplicas", "3")));

        VerifyLogged(logger, LogLevel.Warning, Times.Once());
    }

    [Fact]
    public async Task WithTheCacheOnAndASingleReplica_SaysSoWithoutWarning()
    {
        // EN: The assumption gets written down on every boot, so whoever scales the deployment reads
        //     it. A Warning here instead would be noise on every healthy deployment, and noise is how
        //     warnings stop being read.
        // ES: La suposición queda escrita en cada arranque, así que quien escale el despliegue la lee.
        //     Un Warning acá sería ruido en todo despliegue sano, y el ruido es cómo los warnings
        //     dejan de leerse.
        var logger = await RunCheckAsync(Config(("DbCacheIsEnabled", "true")));

        VerifyLogged(logger, LogLevel.Warning, Times.Never());
        VerifyLogged(logger, LogLevel.Information, Times.Once());
    }

    [Fact]
    public async Task WithTheCacheOff_SaysNothingEvenWithSeveralReplicas()
    {
        // EN: Cache off with N replicas is exactly the recommended mitigation — there is nothing to
        //     warn about.
        // ES: Cache apagada con N réplicas es justamente la mitigación recomendada — no hay nada que
        //     avisar.
        var logger = await RunCheckAsync(Config(("DbCacheIsEnabled", "false"), ("DbCacheReplicas", "3")));

        VerifyLogged(logger, LogLevel.Warning, Times.Never());
    }

    [Fact]
    public async Task WithoutAnyCacheConfiguration_SaysNothing()
    {
        // EN: An app that never turned the cache on should not read about it at startup.
        // ES: Una app que nunca encendió la cache no tiene por qué leer sobre ella al arrancar.
        var logger = await RunCheckAsync(Config());

        VerifyLogged(logger, LogLevel.Warning, Times.Never());
        VerifyLogged(logger, LogLevel.Information, Times.Never());
    }

    [Fact]
    public void CreateBuilder_RegistersTheCheck()
    {
        // EN: CreateBuilder used to register the raw cache, so the check only ran in an app that
        //     remembered to call AddMemoryCacheCustom — that is, almost never.
        // ES: CreateBuilder registraba la cache pelada, así que el chequeo solo corría en una app que
        //     se acordara de llamar a AddMemoryCacheCustom — o sea, casi nunca.
        var builder = ElRoso.Commons.ElRosoApplicationBuilder.CreateBuilder([]);

        Assert.Contains(
            builder.Services,
            d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(DbCacheConfigurationCheck));
    }

    [Fact]
    public void AddMemoryCacheCustom_RegistersTheCheck()
    {
        var services = new ServiceCollection();

        services.AddMemoryCacheCustom();

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(DbCacheConfigurationCheck));
    }
}
