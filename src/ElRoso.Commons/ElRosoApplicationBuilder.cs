// <copyright file="ElRosoApplicationBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Commons;

using ElRoso.DependencyInjection;
using ElRoso.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

public static class ElRosoApplicationBuilder
{
    /// <summary>
    /// EN: Builds the application, translating the one failure that says nothing about its cause.
    ///     <c>ApplicationName</c> is a RESERVED host key: the host reads it as the name of the
    ///     application assembly, so <c>--ApplicationName=Whatever</c> ends up looking for an assembly
    ///     called "Whatever" — the failure surfaces later, when the controllers are registered, as a
    ///     FileNotFoundException that mentions neither the setting nor the command line. Anyone
    ///     passing it means the library's Swagger title / log field, which lives in
    ///     <c>ApplicationDisplayName</c>.
    /// ES: Arma la aplicación, traduciendo la única falla que no dice nada sobre su causa.
    ///     <c>ApplicationName</c> es una clave RESERVADA del host: la lee como el nombre del assembly
    ///     de la aplicación, así que <c>--ApplicationName=LoQueSea</c> termina buscando un assembly
    ///     llamado "LoQueSea" — y la falla aparece más tarde, al registrar los controllers, como un
    ///     FileNotFoundException que no menciona ni la clave ni la línea de comandos. Quien la pasa
    ///     quiere el título de Swagger / campo de log de la librería, que vive en
    ///     <c>ApplicationDisplayName</c>.
    /// </summary>
    /// <param name="args">Command line arguments. / Argumentos de línea de comandos.</param>
    /// <returns>The configured builder. / El builder configurado.</returns>
    [Obsolete("Armar el Host con AddX/UseX; ver README.")]
    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        try
        {
            return CreateBuilderCore(args);
        }
        catch (FileNotFoundException ex) when (PassesTheReservedApplicationName(args))
        {
            throw new InvalidOperationException(
                $"The application could not be built. '{ApplicationDisplayName.ReservedKey}' was passed on the command line, and it is a reserved host key — it names the application assembly, so the host ends up loading an assembly with that name. To set the API title and the log field, use '{ApplicationDisplayName.Key}' instead.",
                ex);
        }
    }

    // EN: Only translate the error when that argument is actually there; every other missing file is
    //     a different problem and deserves its own exception, not this explanation.
    // ES: Solo se traduce el error cuando ese argumento está de verdad; cualquier otro archivo que
    //     falte es otro problema y merece su propia excepción, no esta explicación.
    private static bool PassesTheReservedApplicationName(string[] args) =>
        args.Any(a => a.StartsWith($"--{ApplicationDisplayName.ReservedKey}", StringComparison.OrdinalIgnoreCase)
                   || a.StartsWith($"/{ApplicationDisplayName.ReservedKey}", StringComparison.OrdinalIgnoreCase)
                   || a.Equals(ApplicationDisplayName.ReservedKey, StringComparison.OrdinalIgnoreCase));

    private static WebApplicationBuilder CreateBuilderCore(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddElRosoConfiguration(args);

        // EN: The logger is built here, before the service registrations, so a setup that needs to
        //     warn at startup — like an unrestricted CORS policy — can do it through the app's real
        //     sinks instead of staying silent.
        // ES: El logger se arma acá, antes de los registros, para que una configuración que necesite
        //     avisar en el arranque — como una política CORS sin restringir — pueda hacerlo por los
        //     sinks reales de la app en vez de quedarse callada.
        builder.AddElRosoSerilog(out var startupLogger);

        var startupMsLogger = new Serilog.Extensions.Logging.SerilogLoggerFactory(startupLogger)
            .CreateLogger("ElRoso.Startup");

        // EN: AddMemoryCacheCustom and not AddMemoryCache: the custom one also registers the check
        //     that states, at startup, what this cache assumes about the deployment. Registering the
        //     raw cache here left that check to whoever remembered to call it.
        // ES: AddMemoryCacheCustom y no AddMemoryCache: la versión propia además registra el chequeo
        //     que dice al arrancar qué asume esta cache sobre el despliegue. Registrar la cache
        //     pelada acá dejaba ese chequeo librado a quien se acordara de llamarlo.
        builder.Services.AddMemoryCacheCustom();

        // EN: File storage is opt-in, not automatic — a consumer app picks exactly one of
        //     AddS3BlobStorage / AddAzureBlobStorage (both register IBlobStorageService) and,
        //     separately, AddSharePointFileService if it needs SharePoint. Registering one here
        //     unconditionally would force every app to carry the AWS or Azure Storage SDK even
        //     when it uses neither.
        // ES: El almacenamiento de archivos es opt-in, no automático — cada app consumidora
        //     registra exactamente uno de AddS3BlobStorage / AddAzureBlobStorage (ambos registran
        //     IBlobStorageService) y, aparte, AddSharePointFileService si necesita SharePoint.
        //     Registrar uno acá sin condición forzaría a toda app a cargar el SDK de AWS o Azure
        //     Storage aunque no use ninguno.
        builder.Services.AddMailService();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddHealthChecks();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddApiVersioningCustom(builder.Configuration);
        builder.Services.AddCorsCustom(builder.Configuration, startupMsLogger);
        builder.Services.AddControllersCustom(builder.Configuration);

        builder.Services.AddExceptionHandlerMiddleware();
        builder.Services.AddCurrentUserMiddleware();

        return builder;
    }

    /// <summary>
    /// EN: Mounts the whole HTTP pipeline.
    ///     🔴 Use <paramref name="beforeEndpoints"/> for anything that must run after authentication
    ///     and before the endpoints — above all <c>UseCurrentUserMiddleware()</c>. It is the only
    ///     valid position: this method ends by mapping the endpoints, which terminates the pipeline,
    ///     so a middleware added after the call never executes; and one added before the call runs
    ///     ahead of <c>UseAuthentication</c>, where the caller is not authenticated yet and
    ///     <c>CurrentUserMiddleware</c> passes straight through by design.
    ///
    ///     ⚠️ <c>CurrentUserMiddleware</c> is deliberately NOT mounted by default: it answers 401
    ///     when any of the four Azure AD claims is missing, so mounting it here would break every
    ///     app whose own JWT does not carry them.
    /// ES: Monta todo el pipeline HTTP.
    ///     🔴 Usar <paramref name="beforeEndpoints"/> para todo lo que tenga que correr después de la
    ///     autenticación y antes de los endpoints — sobre todo <c>UseCurrentUserMiddleware()</c>. Es
    ///     la única posición válida: este método termina mapeando los endpoints, lo que cierra el
    ///     pipeline, así que un middleware agregado después de la llamada nunca se ejecuta; y uno
    ///     agregado antes corre delante de <c>UseAuthentication</c>, donde quien llama todavía no
    ///     está autenticado y <c>CurrentUserMiddleware</c> pasa de largo por diseño.
    ///
    ///     ⚠️ <c>CurrentUserMiddleware</c> a propósito NO se monta por defecto: responde 401 si falta
    ///     cualquiera de los cuatro claims de Azure AD, así que montarlo acá rompería toda app cuyo
    ///     propio JWT no los traiga.
    /// </summary>
    /// <param name="app">The application builder. / El builder de la aplicación.</param>
    /// <param name="configuration">Application configuration. / Configuración de la aplicación.</param>
    /// <param name="hostEnvironment">Hosting environment. / Entorno de hosting.</param>
    /// <param name="beforeEndpoints">Runs after authorization, before the endpoints are mapped. / Corre después de la autorización, antes de mapear los endpoints.</param>
    [Obsolete("Armar el Host con AddX/UseX; ver README.")]
    public static void UseElRoso(
        this IApplicationBuilder app,
        IConfiguration configuration,
        IWebHostEnvironment hostEnvironment,
        Action<IApplicationBuilder>? beforeEndpoints = null)
    {
        app.UseIdentityModelPIIInDevelopment(hostEnvironment);

        app.UseExceptionHandlerMiddleware();

        if (configuration.GetValue<bool>("UseHttps"))
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseSwaggerCustom();

        app.UseStaticFiles();
        app.UseCookiePolicy();

        app.UseRouting();
        app.UseRequestLocalization();
        app.UseEnableAllCors();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseResponseCaching();

        app.UseEndpointsCustom(hostEnvironment, beforeEndpoints);
    }

    internal static void AddConfiguration(this IHostApplicationBuilder builder, string[] args) =>
        builder.AddElRosoConfiguration(args);
}
