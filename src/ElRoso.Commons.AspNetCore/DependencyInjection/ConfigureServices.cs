// <copyright file="ConfigureServices.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.DependencyInjection
{
    using System.Text;
    using System.Text.Json.Serialization;
    using Asp.Versioning;
    using ElRoso.Presentation.Filters;
    using ElRoso.Presentation.Middlewares;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.IdentityModel.Tokens;

    public static class ConfigureServices
    {
        public static IServiceCollection AddApiVersioningCustom(this IServiceCollection services, IConfiguration configuration)
        {
            var defaultApiVersion = GetDefaultApiVersion(configuration);

            services
                .AddApiVersioning(options =>
                {
                    options.DefaultApiVersion = defaultApiVersion;
                    options.ReportApiVersions = true;
                })
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });

            return services;
        }

        public static IServiceCollection AddAuthenticationJWT(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtIssuer = configuration.GetValue<string>("JWT:Issuer");
            var jwtAudience = configuration.GetValue<string>("JWT:Audience");
            var jwtIssuerSigningKey = configuration.GetValue<string>("JWT:IssuerSigningKey")
                ?? throw new InvalidOperationException("JWT:IssuerSigningKey is required.");

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    // RequireHttpsMetadata en false por defecto; JWT:RequireHttpsMetadata=true lo exige.
                    options.RequireHttpsMetadata = configuration.GetValue<bool>("JWT:RequireHttpsMetadata", false);
                    options.SaveToken = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ClockSkew = TimeSpan.Zero,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtIssuerSigningKey)),
                        ValidateAudience = true,
                        ValidateIssuer = true,
                        ValidateIssuerSigningKey = true,
                        ValidAudience = jwtAudience,
                        ValidIssuer = jwtIssuer,
                    };
                });

            return services;
        }

        public static IServiceCollection AddCurrentUserMiddleware(this IServiceCollection services)
        {
            services.AddTransient<CurrentUserMiddleware>();

            return services;
        }

        public static IServiceCollection AddExceptionHandlerMiddleware(this IServiceCollection services)
        {
            services.AddTransient<ExceptionHandlerMiddleware>();

            return services;
        }

        public static IServiceCollection AddControllersCustom(this IServiceCollection services, IConfiguration configuration)
        {
            var useJsonStringEnumConverter = configuration.GetValue<bool>("UseJsonStringEnumConverter");

            services.AddControllers(options => options.Filters.Add<UnreadableBodyFilter>())
                .AddJsonOptions(options =>
                {
                    if (useJsonStringEnumConverter)
                    {
                        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    }

                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                });

            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });

            return services;
        }

        /// <summary>
        /// Registra una política CORS con los orígenes de "Cors:AllowedOrigins" (arreglo JSON); sin ellos permite cualquier origen, solo para Development.
        /// </summary>
        /// <returns>La misma colección de servicios.</returns>
        public static IServiceCollection AddCorsCustom(this IServiceCollection services, IConfiguration configuration, ILogger? logger = null)
        {
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

            // Sin orígenes configurados fuera de Development avisa en Warning y permite cualquier origen.
            if (allowedOrigins is not { Length: > 0 })
            {
                var environment = services
                    .FirstOrDefault(d => d.ServiceType == typeof(IHostEnvironment)
                        || d.ServiceType == typeof(Microsoft.AspNetCore.Hosting.IWebHostEnvironment))?
                    .ImplementationInstance as IHostEnvironment;

                var isDevelopment = environment?.EnvironmentName is null
                    || string.Equals(environment.EnvironmentName, Environments.Development, StringComparison.OrdinalIgnoreCase);

                if (!isDevelopment)
                {
                    logger?.LogWarning(
                        "CORS: 'Cors:AllowedOrigins' is not configured in environment '{Environment}', so ANY origin is allowed. Set it before this reaches production.",
                        environment!.EnvironmentName);
                }
            }

            services.AddCors(c =>
            {
                c.AddPolicy(
                    "EnableAllCors",
                    policy =>
                    {
                        policy.AllowAnyHeader().AllowAnyMethod();

                        if (allowedOrigins is { Length: > 0 })
                        {
                            policy.WithOrigins(allowedOrigins);
                        }
                        else
                        {
                            // Fallback: permite cualquier origen (solo adecuado para Desarrollo).
                            policy.AllowAnyOrigin();
                        }
                    });
            });

            return services;
        }

        /// <summary>
        /// Registra una política CORS que permite cualquier origen, método y header; solo para Development o APIs internas.
        /// </summary>
        /// <returns>La misma colección de servicios.</returns>
        [Obsolete("Use AddCorsCustom(services, configuration) and configure Cors:AllowedOrigins in appsettings.")]
        public static IServiceCollection AddEnableAllCors(this IServiceCollection services)
        {
            services.AddCors(c =>
            {
                c.AddPolicy(
                    "EnableAllCors",
                    options =>
                    options
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowAnyOrigin());
            });

            return services;
        }

        private static ApiVersion GetDefaultApiVersion(IConfiguration configuration)
        {
            var versionString = configuration.GetValue<string>("DefaultApiVersion");

            if (string.IsNullOrEmpty(versionString) || !double.TryParse(versionString, out double version))
            {
                return new ApiVersion(1, 0);
            }

            int major = (int)version;
            int minor = (int)((version - major) * 10);

            return new ApiVersion(major, minor);
        }
    }
}
