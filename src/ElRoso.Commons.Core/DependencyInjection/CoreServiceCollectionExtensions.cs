// <copyright file="CoreServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    public static class CoreServiceCollectionExtensions
    {
        /// <summary>
        /// Registra <see cref="ElRoso.Helpers.EncryptionHelper"/> como Singleton.
        /// El work factor de BCrypt se lee de <c>Encryption:WorkFactor</c> (por defecto 12).
        /// </summary>
        /// <returns>La misma colección de servicios.</returns>
        public static IServiceCollection AddEncryptionHelper(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(sp =>
            {
                var workFactor = configuration.GetValue<int?>("Encryption:WorkFactor") ?? 12;
                return new ElRoso.Helpers.EncryptionHelper(workFactor);
            });

            services.TryAddSingleton<ElRoso.Helpers.IPasswordHasher>(
                sp => sp.GetRequiredService<ElRoso.Helpers.EncryptionHelper>());

            return services;
        }
    }
}
