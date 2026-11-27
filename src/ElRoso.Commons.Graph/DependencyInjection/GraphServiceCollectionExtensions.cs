// <copyright file="GraphServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using ElRoso.Services;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

    public static class GraphServiceCollectionExtensions
    {
        /// <summary>
        /// Registra el transporte Graph como singleton.
        /// </summary>
        public static IServiceCollection AddGraphMailService(this IServiceCollection services)
        {
            services.TryAddSingleton<IGraphMailService, GraphMailService>();

            return services;
        }

        public static IServiceCollection AddMailService(this IServiceCollection services)
        {
            services.AddSmtpMailService();
            services.AddGraphMailService();
            services.AddSingleton<IMailService>(provider => new MailService(
                provider.GetRequiredService<ISmtpMailService>(),
                provider.GetRequiredService<IGraphMailService>(),
                provider.GetRequiredService<ILogger<MailService>>()));

            return services;
        }
    }
}
