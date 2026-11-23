namespace ElRoso.Commons.Tests;

using ElRoso.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

public class AzureBlobStorageServiceTests
{
    // EN: A syntactically valid (fake) connection string — the SDK only parses it to build URIs
    //     here, it never touches the network in these tests.
    // ES: Connection string sintácticamente válida (falsa) — el SDK solo la parsea para construir
    //     URIs acá, nunca toca la red en estos tests.
    private const string FakeConnectionString =
        "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net";

    private static AzureBlobStorageService CreateSut(
        string? connectionString = FakeConnectionString,
        string? containerName = "avatars",
        string? createContainer = null,
        string? publicAccess = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AzureBlob:ConnectionString"] = connectionString,
            ["AzureBlob:ContainerName"] = containerName,
            ["AzureBlob:CreateContainerIfNotExists"] = createContainer,
            ["AzureBlob:ContainerPublicAccess"] = publicAccess,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new AzureBlobStorageService(configuration, NullLogger<AzureBlobStorageService>.Instance);
    }

    // EN: Read a private field. These two settings decide whether the service creates containers and
    //     whether they end up publicly readable — they cannot be observed without touching Azure, and
    //     a wrong default is exactly the kind of thing that ships unnoticed.
    // ES: Lee un campo privado. Estos dos settings deciden si el servicio crea contenedores y si
    //     quedan legibles públicamente — no se pueden observar sin tocar Azure, y un default
    //     equivocado es justo lo que se escapa sin que nadie lo note.
    private static T ReadPrivate<T>(AzureBlobStorageService sut, string field) =>
        (T)typeof(AzureBlobStorageService)
            .GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(sut)!;

    [Fact]
    public void Constructor_MissingConnectionString_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => CreateSut(connectionString: null));
    }

    [Fact]
    public void Constructor_MissingContainerName_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => CreateSut(containerName: null));
    }

    [Fact]
    public void Constructor_ValidConfiguration_DoesNotThrow()
    {
        var exception = Record.Exception(() => CreateSut());

        Assert.Null(exception);
    }

    [Fact]
    public void CreateContainer_DefaultsToOff()
    {
        // En producción el contenedor lo crea la infraestructura: la app no debería necesitar
        // permisos de gestión de contenedores solo por estar configurada por defecto.
        Assert.False(ReadPrivate<bool>(CreateSut(), "createContainerIfNotExists"));
    }

    [Fact]
    public void CreateContainer_OptIn_IsHonoured()
    {
        Assert.True(ReadPrivate<bool>(CreateSut(createContainer: "true"), "createContainerIfNotExists"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("none")]
    [InlineData("public")]      // no es el valor esperado: cae al default seguro
    [InlineData("container")]   // listar el contenedor NUNCA se habilita desde acá
    public void PublicAccess_AnythingButBlob_StaysPrivate(string? configured)
    {
        var access = ReadPrivate<Azure.Storage.Blobs.Models.PublicAccessType>(
            CreateSut(publicAccess: configured), "containerPublicAccess");

        Assert.Equal(Azure.Storage.Blobs.Models.PublicAccessType.None, access);
    }

    [Theory]
    [InlineData("blob")]
    [InlineData("Blob")]
    [InlineData("BLOB")]
    public void PublicAccess_Blob_IsCaseInsensitive(string configured)
    {
        var access = ReadPrivate<Azure.Storage.Blobs.Models.PublicAccessType>(
            CreateSut(publicAccess: configured), "containerPublicAccess");

        Assert.Equal(Azure.Storage.Blobs.Models.PublicAccessType.Blob, access);
    }
}
