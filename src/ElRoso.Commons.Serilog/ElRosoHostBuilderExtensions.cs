// <copyright file="ElRosoHostBuilderExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso
{
    using ElRoso.Commons;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Serilog;

    public static class ElRosoHostBuilderExtensions
    {
        public static IHostBuilder UseSerialLogCustom(this IHostBuilder builder, IConfiguration configuration)
        {
            builder

                // Borramos todos los registros de los loggers que vienen prerregistrados
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                })

                // Añadimos Serilog obteniendo la configuración desde Microsoft.Extensions.Configuration
                .UseSerilog(new ElRosoLoggerConfiguration(configuration).CreateLogger());

            return builder;
        }
    }
}
