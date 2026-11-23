// <copyright file="SwaggerSetup.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Swagger
{
    using Asp.Versioning.ApiExplorer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.OpenApi;
    using Swashbuckle.AspNetCore.SwaggerGen;

    public static class SwaggerSetup
    {
        public static void AddSwaggerBasicAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, SwaggerConfigureOptions>();
            services.AddSwaggerGen(c =>
            {
                var securityBearerScheme = new OpenApiSecurityScheme
                {
                    Description = "Enter your username and password as Base64 encoded value in the format 'username:password'.",
                    Type = SecuritySchemeType.Http,
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Scheme = "basic",
                };

                c.AddSecurityDefinition("Basic", securityBearerScheme);
                c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecuritySchemeReference("Basic"),
                        new List<string>()
                    },
                });
            });
        }

        public static void AddSwaggerJWTAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, SwaggerConfigureOptions>();
            services.AddSwaggerGen(c =>
            {
                var securityBearerScheme = new OpenApiSecurityScheme
                {
                    Description = "Authorization using the Bearer scheme. Input token to add to header.",
                    Type = SecuritySchemeType.Http,
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                };
                c.AddSecurityDefinition("Bearer", securityBearerScheme);
                c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecuritySchemeReference("Bearer"),
                        new List<string>()
                    },
                });
            });
        }

        /// <summary>
        /// EN: Mounts Swagger and its UI — but only if the generator was registered.
        ///     🔴 <c>UseElRoso</c> calls this unconditionally, while registering the generator
        ///     (<see cref="AddSwaggerJWTAuthentication"/> / <see cref="AddSwaggerBasicAuthentication"/>)
        ///     is opt-in. An app that skipped it used to mount the middleware over a generator that
        ///     does not exist: mounting worked, and whoever opened /swagger got a 500
        ///     ("Unable to resolve service for type 'ISwaggerProvider'"). Now that combination
        ///     simply leaves Swagger off, with a Warning saying why.
        /// ES: Monta Swagger y su UI — pero solo si el generador fue registrado.
        ///     🔴 <c>UseElRoso</c> llama a esto sin condición, mientras que registrar el generador es
        ///     opt-in. Una app que no lo llamaba montaba el middleware sobre un generador que no
        ///     existe: montar funcionaba, y quien abría /swagger recibía un 500. Ahora esa
        ///     combinación simplemente deja Swagger apagado, con un Warning que dice por qué.
        /// </summary>
        public static void UseSwaggerCustom(this IApplicationBuilder app)
        {
            if (app.ApplicationServices.GetService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>() is null)
            {
                app.ApplicationServices
                    .GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?
                    .CreateLogger("ElRoso.Swagger")
                    .LogWarning(
                        "Swagger is not mounted: its generator was never registered. Call AddSwaggerJWTAuthentication or AddSwaggerBasicAuthentication if you want the documentation.");

                return;
            }

            var apiVersionDescriptionProvider = app.ApplicationServices
                .GetRequiredService<IServiceScopeFactory>()
                .CreateScope()
                .ServiceProvider.GetRequiredService<IApiVersionDescriptionProvider>();

            app.UseSwagger();

            app.UseSwaggerUI(options =>
            {
                foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.ApiVersion.ToString());
                }
            });
        }
    }
}
