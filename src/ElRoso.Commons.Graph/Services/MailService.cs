// <copyright file="MailService.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using Azure.Identity;
    using ElRoso.Domains.Requests;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Graph;
    using Microsoft.Graph.Models;
    using Microsoft.Graph.Users.Item.SendMail;
    using System.Net;
    using System.Net.Mail;

    internal class MailService : ElRosoServiceBase, IMailService
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<MailService> logger;

        // GraphServiceClient is cached at construction — creating it per-call triggers an OAuth2
        // round-trip to Azure on every send operation.
        // El cliente Graph se cachea en construcción — crearlo por llamada dispara un OAuth2
        // round-trip a Azure en cada envío.
        private readonly GraphServiceClient? graphClient;

        public MailService(IConfiguration configuration, ILogger<MailService> logger)
            : base(logger)
        {
            this.configuration = configuration;
            this.logger = logger;

            var tenantId = configuration.GetValue<string>("AzureMailService:TenantId");
            var clientId = configuration.GetValue<string>("AzureMailService:ClientId");
            var clientSecret = configuration.GetValue<string>("AzureMailService:ClientSecret");

            if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
            {
                var credential = new ClientSecretCredential(
                    tenantId,
                    clientId,
                    clientSecret,
                    new ClientSecretCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud });

                this.graphClient = new GraphServiceClient(credential, new List<string> { "https://graph.microsoft.com/.default" });
            }
        }

        // Usa un cliente Graph ya armado en lugar de crearlo desde la configuración.
        internal MailService(IConfiguration configuration, ILogger<MailService> logger, GraphServiceClient graphClient)
            : base(logger)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.graphClient = graphClient;
        }

        public async Task SendGraphAsync(EmailRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (this.graphClient is null)
            {
                throw new InvalidOperationException(
                    "Graph mail is not configured. Verify AzureMailService:TenantId, ClientId, and ClientSecret in appsettings.");
            }

            var toRecipients = request.Recipients
                .Select(x => new Recipient { EmailAddress = new EmailAddress { Address = x } })
                .ToList();

            var ccRecipients = request.CopyRecipients?
                .Select(x => new Recipient { EmailAddress = new EmailAddress { Address = x } })
                .ToList() ?? new List<Recipient>();

            var bccRecipients = request.CarbonCopyRecipients?
                .Select(x => new Recipient { EmailAddress = new EmailAddress { Address = x } })
                .ToList() ?? new List<Recipient>();

            // EN: Attachments are read asynchronously. File.ReadAllBytes inside an async method
            //     blocked a thread-pool thread for as long as the file took to read, which on a
            //     several-megabyte PDF is a thread parked doing nothing but waiting on disk.
            // ES: Los adjuntos se leen de forma asíncrona. File.ReadAllBytes dentro de un método
            //     async bloqueaba un hilo del thread pool todo lo que tardara la lectura, que en un
            //     PDF de varios megas es un hilo estacionado sin hacer nada más que esperar al disco.
            var attachments = new List<Microsoft.Graph.Models.Attachment>();
            if (request.AttachFiles is not null)
            {
                foreach (var file in request.AttachFiles)
                {
                    attachments.Add(new FileAttachment
                    {
                        Name = file.Name,
                        ContentBytes = await File.ReadAllBytesAsync(file.FullName, cancellationToken).ConfigureAwait(false),
                    });
                }
            }

            var requestBody = new SendMailPostRequestBody
            {
                Message = new Message
                {
                    Subject = request.Subject,
                    Body = new ItemBody { ContentType = BodyType.Html, Content = request.Body },
                    ToRecipients = toRecipients,
                    CcRecipients = ccRecipients,
                    BccRecipients = bccRecipients,
                    Attachments = attachments,
                },
                SaveToSentItems = true,
            };

            await this.graphClient
                .Users[request.FromAddress ?? this.configuration.GetValue<string>("AzureMailService:FromAddress")]
                .SendMail
                .PostAsync(requestBody, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            this.logger.LogInformation("Email sent successfully.");
        }

        public async Task SendSMTPAsync(EmailRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var mailMessage = new MailMessage();

            var from = this.configuration.GetValue<string>("SmtpClient:From")
                ?? this.configuration.GetValue<string>("SmtpClient:UserName")
                ?? string.Empty;

            mailMessage.From = new MailAddress(from, this.configuration.GetValue<string>("SmtpClient:DisplayName"));
            mailMessage.Subject = request.Subject;
            mailMessage.Body = request.Body;
            mailMessage.IsBodyHtml = true;

            var replyTo = this.configuration.GetValue<string>("SmtpClient:ReplyTo");
            if (!string.IsNullOrEmpty(replyTo))
            {
                mailMessage.ReplyToList.Add(replyTo);
            }

            request.Recipients.ForEach(x => mailMessage.To.Add(x));
            request.CopyRecipients?.ForEach(x => mailMessage.CC.Add(x));
            request.CarbonCopyRecipients?.ForEach(x => mailMessage.Bcc.Add(x));
            request.AttachFiles?.ForEach(f => mailMessage.Attachments.Add(new System.Net.Mail.Attachment(f.FullName)));

            using var smtpClient = new SmtpClient(
                this.configuration.GetValue<string>("SmtpClient:Host"),
                this.configuration.GetValue<int>("SmtpClient:Port"));

            smtpClient.Credentials = new NetworkCredential(
                this.configuration.GetValue<string>("SmtpClient:UserName"),
                this.configuration.GetValue<string>("SmtpClient:Password"));
            smtpClient.EnableSsl = this.configuration.GetValue<bool>("SmtpClient:EnableSsl");

            await smtpClient.SendMailAsync(mailMessage, cancellationToken).ConfigureAwait(false);
            this.logger.LogInformation("Email sent successfully.");
        }
    }
}
