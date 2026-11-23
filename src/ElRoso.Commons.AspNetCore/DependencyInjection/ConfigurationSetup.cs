// <copyright file="ConfigurationSetup.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using System.Reflection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;

    public static class ConfigurationSetup
    {
        /// <summary>
        /// Reemplaza las fuentes de configuración por appsettings.json, appsettings.{env}.json, user-secrets (solo Development), variables de entorno y línea de comandos, en ese orden.
        /// </summary>
        /// <param name="builder">El builder del host.</param>
        /// <param name="args">Argumentos de línea de comandos.</param>
        /// <returns>El mismo builder.</returns>
        public static IHostApplicationBuilder AddElRosoConfiguration(this IHostApplicationBuilder builder, string[] args)
        {
            builder.Configuration.Sources.Clear();

            IHostEnvironment environment = builder.Environment;

            builder.Configuration
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

            // Carga los user-secrets de la app, antes de las variables de entorno.
            if (environment.IsDevelopment())
            {
                builder.Configuration.AddUserSecrets(Assembly.Load(new AssemblyName(environment.ApplicationName)), optional: true, reloadOnChange: true);
            }

            builder.Configuration
                .AddEnvironmentVariables()
                .AddCommandLine(args);

            return builder;
        }
    }
}
