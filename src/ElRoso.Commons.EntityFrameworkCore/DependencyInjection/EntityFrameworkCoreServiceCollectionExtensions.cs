// <copyright file="EntityFrameworkCoreServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using ElRoso.Services;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    public static class EntityFrameworkCoreServiceCollectionExtensions
    {
        /// <summary>
        /// Registra la cache en proceso de los repositorios y el chequeo de arranque <see cref="DbCacheConfigurationCheck"/>.
        /// </summary>
        /// <param name="services">La colección de servicios.</param>
        /// <returns>La misma colección de servicios.</returns>
        public static IServiceCollection AddMemoryCacheCustom(this IServiceCollection services)
        {
            services
                .AddMemoryCache();

            services.AddSingleton<IHostedService, DbCacheConfigurationCheck>();

            return services;
        }
    }
}
