// <copyright file="MongoDbServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public static class MongoDbServiceCollectionExtensions
    {
        /// <summary>
        /// Registra <see cref="MongoDB.Driver.IMongoClient"/> y <see cref="MongoDB.Driver.IMongoDatabase"/> como Singleton.
        /// </summary>
        /// <returns>La misma colección de servicios.</returns>
        public static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<MongoDB.Driver.IMongoClient>(sp =>
                new MongoDB.Driver.MongoClient(configuration.GetConnectionString("MongoDB")
                    ?? configuration["MongoDb:ConnectionStrings"]
                    ?? throw new InvalidOperationException("MongoDB connection string is not configured.")));

            services.AddSingleton<MongoDB.Driver.IMongoDatabase>(sp =>
            {
                var dbName = configuration["MongoDb:DatabaseName"]
                    ?? throw new InvalidOperationException("MongoDb:DatabaseName is not configured.");
                return sp.GetRequiredService<MongoDB.Driver.IMongoClient>().GetDatabase(dbName);
            });

            return services;
        }
    }
}
