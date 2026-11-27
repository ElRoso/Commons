namespace ElRoso.Commons.Tests;

using ElRoso.DependencyInjection;
using ElRoso.Domains.Requests;
using ElRoso.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public class SmtpMailServiceTests
{
    [Fact]
    public async Task SendAsync_WithCancelledToken_CancelsBeforeReadingSettings()
    {
        ISmtpMailService service = new SmtpMailService(CreateConfiguration(), NullLogger<SmtpMailService>.Instance);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SendAsync(new EmailRequest(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Theory]
    [InlineData("sender@example.com", "sender@example.com")]
    [InlineData(null, "account@example.com")]
    public void CreateMailMessage_MapsSettingsAndRequest(string? configuredFrom, string expectedFrom)
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SmtpClient:From"] = configuredFrom,
            ["SmtpClient:UserName"] = "account@example.com",
            ["SmtpClient:DisplayName"] = "Sender",
            ["SmtpClient:ReplyTo"] = "reply@example.com",
        });
        var service = new SmtpMailService(configuration, NullLogger<SmtpMailService>.Instance);
        var file = Path.GetTempFileName();

        try
        {
            File.WriteAllText(file, "attachment content");
            using var message = service.CreateMailMessage(new EmailRequest
            {
                FromAddress = "ignored@example.com",
                Recipients = ["first@example.com", "second@example.com"],
                CopyRecipients = ["copy@example.com"],
                CarbonCopyRecipients = ["blind-copy@example.com"],
                Subject = "Subject",
                Body = "<p>Body</p>",
                AttachFiles = [new FileInfo(file)],
            });

            Assert.Equal(expectedFrom, message.From?.Address);
            Assert.Equal("Sender", message.From?.DisplayName);
            Assert.Equal("reply@example.com", Assert.Single(message.ReplyToList).Address);
            Assert.Equal(["first@example.com", "second@example.com"], message.To.Select(recipient => recipient.Address));
            Assert.Equal("copy@example.com", Assert.Single(message.CC).Address);
            Assert.Equal("blind-copy@example.com", Assert.Single(message.Bcc).Address);
            Assert.Equal("Subject", message.Subject);
            Assert.Equal("<p>Body</p>", message.Body);
            Assert.True(message.IsBodyHtml);

            var attachment = Assert.Single(message.Attachments);
            Assert.Equal(Path.GetFileName(file), attachment.Name);
            using var reader = new StreamReader(attachment.ContentStream, leaveOpen: true);
            Assert.Equal("attachment content", reader.ReadToEnd());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void CreateMailMessage_WhenAttachmentIsMissing_ReleasesPreviouslyOpenedFile()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SmtpClient:From"] = "sender@example.com",
        });
        var service = new SmtpMailService(configuration, NullLogger<SmtpMailService>.Instance);
        var file = Path.GetTempFileName();

        try
        {
            Assert.Throws<FileNotFoundException>(() =>
            {
                using var message = service.CreateMailMessage(new EmailRequest
                {
                    Recipients = ["recipient@example.com"],
                    AttachFiles = [new FileInfo(file), new FileInfo(file + ".missing")],
                });
            });

            using var reopened = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(reopened.CanWrite);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void AddSmtpMailService_RegistersOnlySmtpAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton(CreateConfiguration());
        services.AddLogging();
        services.AddSmtpMailService();
        services.AddSmtpMailService();

        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, item => item.ServiceType == typeof(ISmtpMailService)).Lifetime);
        using var provider = services.BuildServiceProvider();
        Assert.Same(provider.GetRequiredService<ISmtpMailService>(), provider.GetRequiredService<ISmtpMailService>());
        Assert.Null(provider.GetService<IMailService>());
        Assert.Null(provider.GetService<IGraphMailService>());
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? settings = null)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
