// <copyright file="IGraphMailService.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services;

using ElRoso.Domains.Requests;

/// <summary>
/// Envía correos por Microsoft Graph.
/// </summary>
public interface IGraphMailService
{
    Task SendAsync(EmailRequest request, CancellationToken cancellationToken = default);
}
