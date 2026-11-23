namespace ElRoso.Commons.Tests;

using ElRoso.Domains.Requests;
using ElRoso.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions.Authentication;
using System.Net;
using System.Text;

// Cubre el envío de mail y las operaciones de SharePoint a través del SDK de Graph, con la red simulada.
public class GraphSdkTests
{
    private const string SiteJson = """{"id":"contoso.sharepoint.com,site-guid,web-guid"}""";
    private const string DriveJson = """{"id":"drive-1"}""";
    private const string NotFoundJson = """{"error":{"code":"itemNotFound","message":"The resource could not be found."}}""";

    // ── Mail ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendGraphAsync_PostsTheMessageToTheSenderMailbox()
    {
        var handler = new StubGraphHandler(_ => Respond(HttpStatusCode.Accepted));
        var sut = CreateMail(handler);
        var attachment = new FileInfo(Path.GetTempFileName());
        await File.WriteAllBytesAsync(attachment.FullName, [1, 2, 3]);

        try
        {
            await sut.SendGraphAsync(new EmailRequest
            {
                Subject = "Asunto",
                Body = "<p>Hola</p>",
                Recipients = ["destino@example.com"],
                CopyRecipients = ["visible@example.com"],
                CarbonCopyRecipients = ["oculto@example.com"],
                AttachFiles = [attachment],
            });
        }
        finally
        {
            attachment.Delete();
        }

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/users/no-reply@example.com/sendMail", request.Path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"subject\":\"Asunto\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("destino@example.com", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"ccRecipients\":[{\"emailAddress\":{\"address\":\"visible@example.com\"}}]", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"bccRecipients\":[{\"emailAddress\":{\"address\":\"oculto@example.com\"}}]", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"contentBytes\":\"AQID\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"saveToSentItems\":true", request.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendGraphAsync_WhenGraphRejects_SurfacesTheODataError()
    {
        var handler = new StubGraphHandler(_ => Respond(
            HttpStatusCode.Forbidden,
            """{"error":{"code":"ErrorAccessDenied","message":"Access is denied."}}"""));
        var sut = CreateMail(handler);

        var exception = await Assert.ThrowsAsync<ODataError>(
            () => sut.SendGraphAsync(new EmailRequest { Recipients = ["destino@example.com"] }));

        Assert.Equal(403, exception.ResponseStatusCode);
    }

    // ── SharePoint ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadFileSharePointAsync_PutsTheContentUnderTheFolderOfTheResolvedDrive()
    {
        var handler = new StubGraphHandler(SharePointRoutes(request => request.Method == HttpMethod.Put
            ? Respond(HttpStatusCode.Created, """{"id":"item-1","name":"x.pdf"}""")
            : null));
        var sut = CreateSharePoint(handler);

        var item = await sut.UploadFileSharePointAsync([1, 2, 3], "x.pdf", "carpeta");

        Assert.Equal("item-1", item.Id);
        var put = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Put);
        Assert.EndsWith("/drives/drive-1/root:/carpeta/x.pdf:/content", put.Path, StringComparison.Ordinal);
        Assert.Equal([1, 2, 3], put.Content);
    }

    [Fact]
    public async Task UploadFileSharePointAsync_ResolvesTheDriveOncePerInstance()
    {
        var handler = new StubGraphHandler(SharePointRoutes(request => request.Method == HttpMethod.Put
            ? Respond(HttpStatusCode.Created, """{"id":"item-1"}""")
            : null));
        var sut = CreateSharePoint(handler);

        await sut.UploadFileSharePointAsync([1], "a.pdf");
        await sut.UploadFileSharePointAsync([2], "b.pdf");

        Assert.Single(handler.Requests, r => r.Path.EndsWith("/drive", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UploadFileSharePointAsync_WithoutAFileName_ThrowsBeforeWriting(string fileName)
    {
        var handler = new StubGraphHandler(SharePointRoutes(_ => Respond(HttpStatusCode.Created, """{"id":"item-1"}""")));
        var sut = CreateSharePoint(handler);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.UploadFileSharePointAsync([1], fileName));

        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task DownloadFileSharePointAsync_ReturnsTheContent()
    {
        var handler = new StubGraphHandler(SharePointRoutes(request => request.Path.EndsWith(":/content", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([7, 8, 9]) }
            : null));
        var sut = CreateSharePoint(handler);

        var content = await sut.DownloadFileSharePointAsync("/carpeta/x.pdf");

        Assert.Equal([7, 8, 9], content);
        Assert.Contains(handler.Requests, r => r.Path.EndsWith("/drives/drive-1/root:/carpeta/x.pdf:/content", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DownloadFileSharePointAsync_WhenTheFileDoesNotExist_ThrowsTheDomainNotFound()
    {
        var handler = new StubGraphHandler(SharePointRoutes(request => request.Path.EndsWith(":/content", StringComparison.Ordinal)
            ? Respond(HttpStatusCode.NotFound, NotFoundJson)
            : null));
        var sut = CreateSharePoint(handler);

        await Assert.ThrowsAsync<ElRoso.Exceptions.FileNotFoundException>(() => sut.DownloadFileSharePointAsync("carpeta/x.pdf"));
    }

    [Fact]
    public async Task DeleteFileSharePointAsync_WhenTheFileDoesNotExist_DoesNotThrow()
    {
        var handler = new StubGraphHandler(SharePointRoutes(request => request.Method == HttpMethod.Delete
            ? Respond(HttpStatusCode.NotFound, NotFoundJson)
            : null));
        var sut = CreateSharePoint(handler);

        await sut.DeleteFileSharePointAsync("carpeta/x.pdf");

        var delete = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Delete);
        Assert.EndsWith("/drives/drive-1/root:/carpeta/x.pdf:", delete.Path, StringComparison.Ordinal);
    }

    private static IMailService CreateMail(StubGraphHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AzureMailService:FromAddress"] = "no-reply@example.com" })
            .Build();

        return new MailService(configuration, NullLogger<MailService>.Instance, CreateClient(handler));
    }

    private static ISharePointFileService CreateSharePoint(StubGraphHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureSharePoint:Site"] = "contoso.sharepoint.com",
                ["AzureSharePoint:SubSite"] = "documentos",
            })
            .Build();

        return new SharePointFileService(configuration, NullLogger<SharePointFileService>.Instance, CreateClient(handler));
    }

    private static GraphServiceClient CreateClient(StubGraphHandler handler) =>
        new(new HttpClient(handler), new AnonymousAuthenticationProvider());

    // Responde la resolución del sitio y el drive; el resto lo decide la operación bajo prueba.
    private static Func<RecordedRequest, HttpResponseMessage> SharePointRoutes(Func<RecordedRequest, HttpResponseMessage?> operation) =>
        request =>
        {
            if (request.Path.EndsWith("/sites/site-guid/drive", StringComparison.Ordinal))
            {
                return Respond(HttpStatusCode.OK, DriveJson);
            }

            if (request.Path.Contains("/sites/contoso.sharepoint.com:/sites/documentos", StringComparison.Ordinal))
            {
                return Respond(HttpStatusCode.OK, SiteJson);
            }

            return operation(request) ?? Respond(HttpStatusCode.InternalServerError);
        };

    private static HttpResponseMessage Respond(HttpStatusCode status, string? json = null) =>
        new(status)
        {
            Content = json is null ? new ByteArrayContent([]) : new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed record RecordedRequest(HttpMethod Method, string Path, byte[] Content)
    {
        public string Body => Encoding.UTF8.GetString(this.Content);
    }

    private sealed class StubGraphHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var recorded = new RecordedRequest(request.Method, Uri.UnescapeDataString(request.RequestUri!.AbsolutePath), content);
            this.Requests.Add(recorded);
            return respond(recorded);
        }
    }
}
