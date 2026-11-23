// <copyright file="AzureBlobServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using ElRoso.Services;
    using Microsoft.Extensions.DependencyInjection;

    public static class AzureBlobServiceCollectionExtensions
    {
        /// <summary>
        /// Registra <see cref="IBlobStorageService"/> sobre Azure Blob Storage leyendo <c>AzureBlob:*</c> de la configuración.
        /// Excluyente con <c>AddS3BlobStorage</c>.
        /// </summary>
        /// <returns>La misma colección de servicios.</returns>
        public static IServiceCollection AddAzureBlobStorage(this IServiceCollection services)
        {
            services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

            return services;
        }
    }
}
