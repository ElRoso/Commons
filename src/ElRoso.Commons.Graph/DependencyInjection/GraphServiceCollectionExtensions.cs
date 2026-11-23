// <copyright file="GraphServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using ElRoso.Services;
    using Microsoft.Extensions.DependencyInjection;

    public static class GraphServiceCollectionExtensions
    {
        public static IServiceCollection AddMailService(this IServiceCollection services)
        {
            services.AddSingleton<IMailService, MailService>();

            return services;
        }
    }
}
