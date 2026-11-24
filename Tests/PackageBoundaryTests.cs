namespace ElRoso.Commons.Tests;

using System.Reflection;
using ElRoso.DependencyInjection;
using ElRoso.Domains.Responses;
using ElRoso.Exceptions;
using ElRoso.Helpers;
using ElRoso.Presentation.Middlewares;
using ElRoso.Providers.NoSQL;
using ElRoso.Outcomes;
using ElRoso.Providers.SQL;
using ElRoso.Services;
using ElRoso.Swagger;

// Cada tipo y cada extensión vive en el paquete de su área, y los paquetes base no arrastran SDKs de terceros.
public class PackageBoundaryTests
{
    private static readonly string[] ForbiddenInBasePackages =
    [
        "MongoDB.",
        "Microsoft.Graph",
        "AWSSDK.",
        "Azure.",
        "Microsoft.Identity.Web",
        "Microsoft.Data.SqlClient",
        "Polly",
        "Microsoft.Extensions.Hosting.WindowsServices",
    ];

    public static TheoryData<Type, string> Types => new()
    {
        { typeof(CustomRandom), "ElRoso.Commons.Core" },
        { typeof(EncryptionHelper), "ElRoso.Commons.Core" },
        { typeof(CronJobService), "ElRoso.Commons.Core" },
        { typeof(BadRequestException), "ElRoso.Commons.Core" },
        { typeof(ProblemResponse), "ElRoso.Commons.Core" },
        { typeof(Result), "ElRoso.Commons.Core" },
        { typeof(Error), "ElRoso.Commons.Core" },
        { typeof(IBlobStorageService), "ElRoso.Commons.Core" },
        { typeof(IMailService), "ElRoso.Commons.Core" },
        { typeof(ExceptionHandlerMiddleware), "ElRoso.Commons.AspNetCore" },
        { typeof(ForwardedHeadersSetup), "ElRoso.Commons.AspNetCore" },
        { typeof(HealthCheckSetup), "ElRoso.Commons.AspNetCore" },
        { typeof(SwaggerSetup), "ElRoso.Commons.Swagger" },
        { typeof(ElRosoRepositoryBase<>), "ElRoso.Commons.EntityFrameworkCore" },
        { typeof(ElRoso.Domains.Entities.SQL.BaseEntity), "ElRoso.Commons.EntityFrameworkCore" },
        { typeof(Repository<>), "ElRoso.Commons.MongoDB" },
        { typeof(ElRoso.Domains.Entities.NoSQL.BaseEntity), "ElRoso.Commons.MongoDB" },
        { typeof(ISharePointFileService), "ElRoso.Commons.SharePoint" },
        { typeof(ITelegramBot), "ElRoso.Commons.Telegram" },
        { typeof(TelegramRateLimitException), "ElRoso.Commons.Telegram" },
        { typeof(ElRosoApplicationBuilder), "ElRoso.Commons" },
    };

    public static TheoryData<string, string> Extensions => new()
    {
        { "AddEncryptionHelper", "ElRoso.Commons.Core" },
        { "AddAuthenticationJWT", "ElRoso.Commons.AspNetCore" },
        { "AddControllersCustom", "ElRoso.Commons.AspNetCore" },
        { "AddElRosoProblemDetails", "ElRoso.Commons.AspNetCore" },
        { "UseElRosoProblemDetails", "ElRoso.Commons.AspNetCore" },
        { "ToHttpResult", "ElRoso.Commons.AspNetCore" },
        { "UseSerialLogCustom", "ElRoso.Commons.Serilog" },
        { "UseSwaggerCustom", "ElRoso.Commons.Swagger" },
        { "AddMemoryCacheCustom", "ElRoso.Commons.EntityFrameworkCore" },
        { "AddMongoDb", "ElRoso.Commons.MongoDB" },
        { "AddAuthenticationAzureAD", "ElRoso.Commons.AzureAD" },
        { "AddMailService", "ElRoso.Commons.Graph" },
        { "AddSharePointFileService", "ElRoso.Commons.SharePoint" },
        { "AddS3BlobStorage", "ElRoso.Commons.S3" },
        { "AddAzureBlobStorage", "ElRoso.Commons.AzureBlob" },
        { "AddTelegramBot", "ElRoso.Commons.Telegram" },
        { "AddTelegramNotifier", "ElRoso.Commons.Telegram" },
        { "AddTelegramPolling", "ElRoso.Commons.Telegram" },
        { "UseWindowsServiceCustom", "ElRoso.Commons.WindowsServices" },
    };

    public static TheoryData<string> BasePackages => new()
    {
        "ElRoso.Commons.Core",
        "ElRoso.Commons.AspNetCore",
        "ElRoso.Commons.Serilog",
        "ElRoso.Commons.EntityFrameworkCore",
    };

    [Theory]
    [MemberData(nameof(Types))]
    public void EachTypeLivesInTheAssemblyOfItsArea(Type type, string expectedAssembly)
    {
        Assert.Equal(expectedAssembly, type.Assembly.GetName().Name);
    }

    [Theory]
    [MemberData(nameof(Extensions))]
    public void EachExtensionLivesInTheAssemblyOfItsArea(string method, string expectedAssembly)
    {
        var declaring = LoadLibraryAssemblies()
            .Where(a => a.GetExportedTypes().Any(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static).Any(m => m.Name == method)))
            .Select(a => a.GetName().Name)
            .ToList();

        Assert.Equal([expectedAssembly], declaring);
    }

    [Theory]
    [MemberData(nameof(BasePackages))]
    public void BasePackagesDoNotReferenceThirdPartySdks(string assemblyName)
    {
        var assembly = LoadLibraryAssemblies().SingleOrDefault(a => a.GetName().Name == assemblyName);
        Assert.NotNull(assembly);

        var forbidden = assembly.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Where(name => ForbiddenInBasePackages.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(forbidden);
    }

    // Carga todos los ensamblados ElRoso.Commons* copiados junto a los tests.
    private static List<Assembly> LoadLibraryAssemblies() =>
        Directory.GetFiles(AppContext.BaseDirectory, "ElRoso.Commons*.dll")
            .Select(path => AssemblyName.GetAssemblyName(path))
            .Where(name => name.Name != "ElRoso.Commons.Tests")
            .Select(Assembly.Load)
            .ToList();
}
