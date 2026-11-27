// <copyright file="MailService.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using ElRoso.Domains.Requests;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Graph;

    internal class MailService : ElRosoServiceBase, IMailService
    {
        private readonly ISmtpMailService smtpMailService;
        private readonly IGraphMailService graphMailService;

        public MailService(IConfiguration configuration, ILogger<MailService> logger)
            : this(new SmtpMailService(configuration, logger), new GraphMailService(configuration, logger), logger)
        {
        }

        internal MailService(IConfiguration configuration, ILogger<MailService> logger, GraphServiceClient graphClient)
            : this(new SmtpMailService(configuration, logger), new GraphMailService(configuration, logger, graphClient), logger)
        {
        }

        internal MailService(ISmtpMailService smtpMailService, IGraphMailService graphMailService, ILogger<MailService> logger)
            : base(logger)
        {
            this.smtpMailService = smtpMailService;
            this.graphMailService = graphMailService;
        }

        public Task SendGraphAsync(EmailRequest request, CancellationToken cancellationToken = default)
        {
            return this.graphMailService.SendAsync(request, cancellationToken);
        }

        public Task SendSMTPAsync(EmailRequest request, CancellationToken cancellationToken = default)
        {
            return this.smtpMailService.SendAsync(request, cancellationToken);
        }
    }
}
