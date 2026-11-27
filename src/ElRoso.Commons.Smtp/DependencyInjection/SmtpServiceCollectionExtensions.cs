// <copyright file="SmtpServiceCollectionExtensions.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection;

using ElRoso.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class SmtpServiceCollectionExtensions
{
    /// <summary>
    /// Registra el transporte SMTP como singleton.
    /// </summary>
    public static IServiceCollection AddSmtpMailService(this IServiceCollection services)
    {
        services.TryAddSingleton<ISmtpMailService, SmtpMailService>();

        return services;
    }
}
