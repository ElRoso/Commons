namespace ElRoso.Commons.Tests;

using Amazon;
using ElRoso.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

public class S3BlobStorageServiceTests
{
    private static S3BlobStorageService CreateSut(string? region = "us-east-1", string? serviceUrl = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AWS:AccessKeyId"] = "fake-key",
            ["AWS:SecretAccessKey"] = "fake-secret",
            ["AWS:BucketName"] = "my-bucket",
            ["AWS:Region"] = region,
            ["AWS:ServiceURL"] = serviceUrl,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new S3BlobStorageService(configuration, NullLogger<S3BlobStorageService>.Instance);
    }

    [Fact]
    public void BuildUrl_ReturnsVirtualHostedStyleUrl()
    {
        var url = S3BlobStorageService.BuildUrl("my-bucket", RegionEndpoint.USEast1, "user123.png");

        Assert.Equal("https://my-bucket.s3.us-east-1.amazonaws.com/user123.png", url);
    }

    [Fact]
    public void BuildUrl_UsesTheGivenRegion()
    {
        var url = S3BlobStorageService.BuildUrl("my-bucket", RegionEndpoint.SAEast1, "file.png");

        Assert.Contains(".s3.sa-east-1.amazonaws.com/", url);
    }

    [Fact]
    public void BuildUrl_EscapesSpacesInFileName()
    {
        var url = S3BlobStorageService.BuildUrl("my-bucket", RegionEndpoint.USEast1, "my file.png");

        Assert.Equal("https://my-bucket.s3.us-east-1.amazonaws.com/my%20file.png", url);
    }

    [Fact]
    public void BuildUrl_PreservesFolderLikeSlashesInTheKey()
    {
        // EN: "/" must survive as a path separator — Uri.EscapeDataString alone would turn it into
        //     %2F and corrupt the S3 "folder" structure.
        var url = S3BlobStorageService.BuildUrl("my-bucket", RegionEndpoint.USEast1, "avatars/user123.png");

        Assert.Equal("https://my-bucket.s3.us-east-1.amazonaws.com/avatars/user123.png", url);
    }

    // EN: The AWS client owns an HTTP handler and a connection pool. Building one per call threw
    //     that away on every upload and download, in a service that DI registers as a singleton
    //     precisely so its expensive parts get built once.
    // ES: El cliente de AWS es dueño de un handler HTTP y un pool de conexiones. Armar uno por
    //     llamada tiraba todo eso en cada subida y bajada, en un servicio que DI registra como
    //     singleton justamente para que lo caro se arme una sola vez.
    [Fact]
    public void S3Client_IsBuiltOnceAndReused()
    {
        var sut = CreateSut();

        var first = ReadPrivateClient(sut);
        var second = ReadPrivateClient(sut);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    // ── S3-compatible endpoints (R2, MinIO) ──────────────────────────────────────────────────

    // EN: 🔴 Without ForcePathStyle the SDK builds virtual-hosted urls
    //     (https://{bucket}.{host}/{key}) that a non-AWS S3-compatible endpoint does not resolve.
    //     This is the single setting that decides whether the whole client talks to R2 or to
    //     nothing at all, and it is silent: the config is accepted and every call fails at DNS.
    // ES: 🔴 Sin ForcePathStyle el SDK arma urls virtual-hosted
    //     (https://{bucket}.{host}/{key}) que un endpoint S3-compatible no-AWS no resuelve. Es el
    //     único setting que decide si el cliente entero le habla a R2 o a nada, y es silencioso: la
    //     config se acepta y cada llamada falla en el DNS.
    [Fact]
    public void S3Client_WithAServiceUrl_ForcesPathStyle()
    {
        var sut = CreateSut(region: "auto", serviceUrl: "https://accountid.r2.cloudflarestorage.com");

        var config = ReadClientConfig(sut);

        Assert.True(config.ForcePathStyle);
        Assert.Equal("https://accountid.r2.cloudflarestorage.com", config.ServiceURL.TrimEnd('/'));
    }

    // EN: The AWS path stays exactly as it was — no ServiceURL means the regional endpoint and the
    //     virtual-hosted urls AWS recommends. This is what keeps the change additive.
    // ES: El camino de AWS queda igual — sin ServiceURL, endpoint regional y urls virtual-hosted,
    //     que es lo que AWS recomienda. Esto es lo que mantiene el cambio aditivo.
    [Fact]
    public void S3Client_WithoutAServiceUrl_KeepsTheRegionalEndpoint()
    {
        var sut = CreateSut();

        var config = ReadClientConfig(sut);

        Assert.False(config.ForcePathStyle);
        Assert.Equal(RegionEndpoint.USEast1, config.RegionEndpoint);
    }

    // EN: R2 has no regions and its documented value is the literal "auto". The constructor used to
    //     hand every region name to RegionEndpoint.GetBySystemName; if that refuses "auto", pointing
    //     at R2 is impossible no matter what else is configured.
    // ES: R2 no tiene regiones y su valor documentado es el literal "auto". El constructor le pasaba
    //     todo nombre de región a RegionEndpoint.GetBySystemName; si eso rechaza "auto", apuntar a
    //     R2 es imposible por más que el resto esté bien configurado.
    [Fact]
    public void Constructor_WithAServiceUrl_AcceptsTheAutoRegion()
    {
        var exception = Record.Exception(() =>
            CreateSut(region: "auto", serviceUrl: "https://accountid.r2.cloudflarestorage.com"));

        Assert.Null(exception);
    }

    // Con ServiceUrl la url devuelta usa path-style.
    [Fact]
    public void BuildUrl_WithAServiceUrl_ReturnsPathStyleUrl()
    {
        var url = S3BlobStorageService.BuildUrl(
            "my-bucket",
            RegionEndpoint.USEast1,
            "user123.png",
            "https://accountid.r2.cloudflarestorage.com");

        Assert.Equal("https://accountid.r2.cloudflarestorage.com/my-bucket/user123.png", url);
    }

    [Fact]
    public void BuildUrl_WithAServiceUrl_DoesNotDoubleTheSeparator()
    {
        var url = S3BlobStorageService.BuildUrl(
            "my-bucket",
            RegionEndpoint.USEast1,
            "user123.png",
            "https://accountid.r2.cloudflarestorage.com/");

        Assert.Equal("https://accountid.r2.cloudflarestorage.com/my-bucket/user123.png", url);
    }

    // EN: Same escaping rule as the AWS path: segments escaped one by one so a "folder-like" key
    //     survives as a path instead of collapsing into one opaque segment.
    // ES: Misma regla de escapado que el camino de AWS: segmentos escapados de a uno para que una
    //     key "tipo carpeta" sobreviva como path en vez de colapsar en un segmento opaco.
    [Fact]
    public void BuildUrl_WithAServiceUrl_PreservesFolderLikeSlashesAndEscapesSpaces()
    {
        var url = S3BlobStorageService.BuildUrl(
            "my-bucket",
            RegionEndpoint.USEast1,
            "avatars/mi archivo.png",
            "https://accountid.r2.cloudflarestorage.com");

        Assert.Equal("https://accountid.r2.cloudflarestorage.com/my-bucket/avatars/mi%20archivo.png", url);
    }

    private static Amazon.S3.AmazonS3Config ReadClientConfig(S3BlobStorageService sut) =>
        (Amazon.S3.AmazonS3Config)((Amazon.S3.IAmazonS3)ReadPrivateClient(sut)!).Config;

    private static object? ReadPrivateClient(S3BlobStorageService sut) =>
        typeof(S3BlobStorageService)
            .GetField("client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(sut);
}
