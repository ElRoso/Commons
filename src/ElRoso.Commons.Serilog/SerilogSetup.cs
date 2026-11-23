// <copyright file="SerilogSetup.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using ElRoso.Commons;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Serilog;

    public static class SerilogSetup
    {
        /// <summary>
        /// Reemplaza los proveedores de logging por el logger Serilog armado con la configuración actual.
        /// </summary>
        /// <param name="builder">El builder del host.</param>
        /// <returns>El mismo builder.</returns>
        public static IHostApplicationBuilder AddElRosoSerilog(this IHostApplicationBuilder builder) =>
            builder.AddElRosoSerilog(out _);

        /// <summary>
        /// Igual que <see cref="AddElRosoSerilog(IHostApplicationBuilder)"/> y devuelve el logger instalado.
        /// </summary>
        /// <param name="builder">El builder del host.</param>
        /// <param name="logger">El logger instalado.</param>
        /// <returns>El mismo builder.</returns>
        public static IHostApplicationBuilder AddElRosoSerilog(this IHostApplicationBuilder builder, out Serilog.ILogger logger)
        {
            logger = new ElRosoLoggerConfiguration(builder.Configuration).CreateLogger();

            builder.Logging.ClearProviders();
            builder.Services.AddSerilog(logger);

            return builder;
        }
    }
}
