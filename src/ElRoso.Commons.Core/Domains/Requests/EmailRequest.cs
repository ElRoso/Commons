// <copyright file="EmailRequest.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Domains.Requests
{
    /// <summary>
    /// Request model for sending emails via SMTP or Microsoft Graph.
    /// Modelo de solicitud para envío de correos electrónicos via SMTP o Microsoft Graph.
    /// </summary>
    public class EmailRequest : BaseRequest
    {
        public string? FromAddress { get; set; }

        public List<string> Recipients { get; set; } = [];

        public List<string>? CopyRecipients { get; set; }

        public List<string>? CarbonCopyRecipients { get; set; }

        public string Subject { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        public List<FileInfo>? AttachFiles { get; set; }
    }
}
