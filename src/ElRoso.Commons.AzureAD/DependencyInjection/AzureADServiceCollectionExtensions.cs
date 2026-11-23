// <copyright file="AzureADServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Identity.Web;

    public static class AzureADServiceCollectionExtensions
    {
        public static IServiceCollection AddAuthenticationAzureAD(this IServiceCollection services, IConfiguration configuration)
        {
            var azureAdConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>()
                {
                    ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                    ["AzureAd:ClientId"] = configuration.GetValue<string>("AzureAd:ClientId"),
                    ["AzureAd:TenantId"] = configuration.GetValue<string>("AzureAd:TenantId"),
                })
                .Build();

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddMicrosoftIdentityWebApi(azureAdConfig);

            return services;
        }
    }
}
