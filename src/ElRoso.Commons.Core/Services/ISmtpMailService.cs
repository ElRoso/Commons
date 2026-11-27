// <copyright file="ISmtpMailService.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services;

using ElRoso.Domains.Requests;

/// <summary>
/// Envía correos por SMTP.
/// </summary>
public interface ISmtpMailService
{
    Task SendAsync(EmailRequest request, CancellationToken cancellationToken = default);
}
