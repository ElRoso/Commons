// <copyright file="Argon2ServiceCollectionExtensions.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection;

using ElRoso.Argon2;
using ElRoso.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registro de <see cref="Argon2idPasswordHasher"/> en el contenedor de servicios.
/// </summary>
public static class Argon2ServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="Argon2idPasswordHasher"/> como <see cref="IPasswordHasher"/> Singleton y reemplaza
    /// cualquier registro previo. Lee <c>PasswordHashing:Argon2:MemoryKiB</c>, <c>:Iterations</c> y <c>:Parallelism</c>.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <returns>La misma colección de servicios.</returns>
    public static IServiceCollection AddArgon2PasswordHasher(this IServiceCollection services, IConfiguration configuration)
    {
        var defaults = new Argon2idOptions();
        var options = new Argon2idOptions
        {
            MemoryKiB = configuration.GetValue<int?>("PasswordHashing:Argon2:MemoryKiB") ?? defaults.MemoryKiB,
            Iterations = configuration.GetValue<int?>("PasswordHashing:Argon2:Iterations") ?? defaults.Iterations,
            Parallelism = configuration.GetValue<int?>("PasswordHashing:Argon2:Parallelism") ?? defaults.Parallelism,
        };

        services.Replace(ServiceDescriptor.Singleton<IPasswordHasher>(new Argon2idPasswordHasher(options)));

        return services;
    }
}
