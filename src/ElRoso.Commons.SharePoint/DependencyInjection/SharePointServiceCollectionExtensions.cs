// <copyright file="SharePointServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using ElRoso.Services;
    using Microsoft.Extensions.DependencyInjection;

    public static class SharePointServiceCollectionExtensions
    {
        /// <summary>
        /// Registra <see cref="ISharePointFileService"/> leyendo <c>AzureSharePoint:*</c> de la configuración.
        /// Sin credenciales, cada llamada lanza excepción en vez de fallar el arranque.
        /// </summary>
        /// <returns>La misma colección de servicios.</returns>
        public static IServiceCollection AddSharePointFileService(this IServiceCollection services)
        {
            services.AddSingleton<ISharePointFileService, SharePointFileService>();

            return services;
        }
    }
}
