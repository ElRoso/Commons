// <copyright file="GraphMailService.cs" company="El Roso">
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

    internal sealed class GraphMailService : IGraphMailService
    {
        private readonly IConfiguration configuration;
        private readonly ILogger logger;

        private readonly GraphServiceClient? graphClient;

        public GraphMailService(IConfiguration configuration, ILogger<GraphMailService> logger)
            : this(configuration, (ILogger)logger)
        {
        }

        internal GraphMailService(IConfiguration configuration, ILogger logger)
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

        internal GraphMailService(IConfiguration configuration, ILogger logger, GraphServiceClient graphClient)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.graphClient = graphClient;
        }

        public async Task SendAsync(EmailRequest request, CancellationToken cancellationToken = default)
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
    }
}
