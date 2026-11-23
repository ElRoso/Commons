// <copyright file="S3ServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using ElRoso.Services;
    using Microsoft.Extensions.DependencyInjection;

    public static class S3ServiceCollectionExtensions
    {
        /// <summary>
        /// Registra <see cref="IBlobStorageService"/> sobre AWS S3 leyendo <c>AWS:*</c> de la configuración; <c>AWS:ServiceURL</c> apunta a un endpoint S3-compatible con path-style.
        /// Excluyente con <c>AddAzureBlobStorage</c>.
        /// </summary>
        /// <returns>La misma colección de servicios.</returns>
        public static IServiceCollection AddS3BlobStorage(this IServiceCollection services)
        {
            services.AddSingleton<IBlobStorageService, S3BlobStorageService>();

            return services;
        }
    }
}
