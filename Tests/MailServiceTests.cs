namespace ElRoso.Commons.Tests;

using ElRoso.Domains.Requests;
using ElRoso.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

public class MailServiceTests
{
    private static IMailService CreateSut(bool withGraphCredentials = false, string? smtpHost = null)
    {
        var settings = new Dictionary<string, string?>();

        if (withGraphCredentials)
        {
            settings["AzureMailService:TenantId"] = "00000000-0000-0000-0000-000000000000";
            settings["AzureMailService:ClientId"] = "00000000-0000-0000-0000-000000000001";
            settings["AzureMailService:ClientSecret"] = "fake-secret";
            settings["AzureMailService:FromAddress"] = "no-reply@elroso.ar";
        }

        if (smtpHost is not null)
        {
            settings["SmtpClient:Host"] = smtpHost;
            settings["SmtpClient:Port"] = "2525";
            settings["SmtpClient:From"] = "no-reply@elroso.ar";
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new MailService(configuration, NullLogger<MailService>.Instance);
    }

    [Fact]
    public async Task SendGraphAsync_WithoutCredentials_SaysWhatIsMissing()
    {
        var sut = CreateSut(withGraphCredentials: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SendGraphAsync(new EmailRequest { Recipients = ["a@b.com"] }));

        Assert.Contains("AzureMailService", exception.Message);
    }

    [Fact]
    public async Task SendGraphAsync_WithACancelledToken_DoesNotAttemptTheSend()
    {
        // EN: Until 2026-08-05 no method of IMailService took a CancellationToken, so a send that
        //     hung could not be cut — the caller waited on the SDK's own timeout with no way out.
        // ES: Hasta el 2026-08-05 ningún método de IMailService recibía CancellationToken, así que
        //     un envío colgado no se podía cortar — quien llamaba esperaba el timeout del SDK sin
        //     salida.
        var sut = CreateSut(withGraphCredentials: true);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.SendGraphAsync(new EmailRequest { Recipients = ["a@b.com"] }, cts.Token));
    }

    [Fact]
    public async Task SendSMTPAsync_WithACancelledToken_DoesNotAttemptTheSend()
    {
        var sut = CreateSut(smtpHost: "localhost");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.SendSMTPAsync(new EmailRequest { Recipients = ["a@b.com"] }, cts.Token));
    }

    [Fact]
    public async Task SendGraphAsync_WithAMissingAttachment_FailsBeforeReachingTheNetwork()
    {
        // EN: Attachments used to be read with File.ReadAllBytes inside an async method, blocking a
        //     thread-pool thread for the size of the file. They are read asynchronously now; a
        //     missing file still fails, and it must fail clearly.
        // ES: Los adjuntos se leían con File.ReadAllBytes dentro de un método async, bloqueando un
        //     hilo del thread pool en proporción al tamaño del archivo. Ahora se leen de forma
        //     asíncrona; un archivo inexistente sigue fallando, y tiene que fallar claro.
        var sut = CreateSut(withGraphCredentials: true);
        var request = new EmailRequest
        {
            Recipients = ["a@b.com"],
            AttachFiles = [new FileInfo("/tmp/no-existe-este-archivo-elroso.pdf")],
        };

        await Assert.ThrowsAnyAsync<FileNotFoundException>(() => sut.SendGraphAsync(request));
    }

    [Fact]
    public void EmailRequest_CarbonCopyRecipients_MapsToBcc()
    {
        // EN: Documents the trap in the name: CarbonCopyRecipients is the BLIND copy. CopyRecipients
        //     is the visible one.
        // ES: Documenta la trampa del nombre: CarbonCopyRecipients es la copia OCULTA.
        //     CopyRecipients es la visible.
        var request = new EmailRequest
        {
            Recipients = ["destino@elroso.ar"],
            CopyRecipients = ["visible@elroso.ar"],
            CarbonCopyRecipients = ["oculto@elroso.ar"],
        };

        Assert.Single(request.CopyRecipients);
        Assert.Single(request.CarbonCopyRecipients);
    }
}
