// <copyright file="WindowsServiceHostBuilderExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso
{
    using Microsoft.Extensions.Hosting;
    using System.Reflection;

    public static class WindowsServiceHostBuilderExtensions
    {
        public static IHostBuilder UseWindowsServiceCustom(this IHostBuilder builder)
        {
            builder
                .UseWindowsService(configure =>
                {
                    configure.ServiceName = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;
                });

            return builder;
        }
    }
}
