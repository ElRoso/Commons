// <copyright file="SmtpMailService.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services;

using System.Net;
using System.Net.Mail;
using ElRoso.Domains.Requests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

internal sealed class SmtpMailService : ISmtpMailService
{
    private readonly IConfiguration configuration;
    private readonly ILogger logger;

    public SmtpMailService(IConfiguration configuration, ILogger<SmtpMailService> logger)
        : this(configuration, (ILogger)logger)
    {
    }

    internal SmtpMailService(IConfiguration configuration, ILogger logger)
    {
        this.configuration = configuration;
        this.logger = logger;
    }

    public async Task SendAsync(EmailRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var mailMessage = this.CreateMailMessage(request);
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

    internal MailMessage CreateMailMessage(EmailRequest request)
    {
        var mailMessage = new MailMessage();

        try
        {
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
            request.AttachFiles?.ForEach(f => mailMessage.Attachments.Add(new Attachment(f.FullName)));

            return mailMessage;
        }
        catch
        {
            mailMessage.Dispose();
            throw;
        }
    }
}
