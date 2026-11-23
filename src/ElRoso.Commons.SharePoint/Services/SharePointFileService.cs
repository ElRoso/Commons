// <copyright file="SharePointFileService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using Azure.Identity;
    using ElRoso.Exceptions;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Graph;
    using Microsoft.Graph.Models;

    internal class SharePointFileService : ElRosoServiceBase, ISharePointFileService
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<SharePointFileService> logger;

        // Credentials and GraphServiceClient are cached at construction — creating them per-call
        // triggers an OAuth2 round-trip to Azure on every operation.
        // Las credenciales y el cliente Graph se cachean en construcción — crearlos por llamada
        // dispara un round-trip OAuth2 a Azure en cada operación.
        private readonly ClientSecretCredential? sharePointCredential;
        private readonly GraphServiceClient? sharePointClient;

        // EN: Site and drive IDs are stable for the life of the site, so they are resolved once per
        //     process instead of on every file operation.
        // ES: Los IDs de sitio y drive son estables por vida del sitio, así que se resuelven una vez
        //     por proceso en lugar de en cada operación con archivos.
        private readonly SemaphoreSlim driveGate = new(1, 1);
        private (string SiteId, string DriveId)? cachedDrive;

        public SharePointFileService(IConfiguration configuration, ILogger<SharePointFileService> logger)
            : base(logger)
        {
            this.configuration = configuration;
            this.logger = logger;

            var tenantId = configuration.GetValue<string>("AzureSharePoint:TenantId");
            var clientId = configuration.GetValue<string>("AzureSharePoint:ClientId");
            var clientSecret = configuration.GetValue<string>("AzureSharePoint:ClientSecret");

            if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
            {
                this.sharePointCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                this.sharePointClient = new GraphServiceClient(this.sharePointCredential);
            }
        }

        // Usa un cliente Graph ya armado en lugar de crearlo desde la configuración.
        internal SharePointFileService(IConfiguration configuration, ILogger<SharePointFileService> logger, GraphServiceClient sharePointClient)
            : base(logger)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.sharePointClient = sharePointClient;
        }

        public async Task<DriveItem> UploadFileSharePointAsync(FileInfo file, string? folderSharePoint = null, string? newFileName = null)
        {
            this.EnsureSharePointConfigured();

            var (_, driveId) = await this.GetSharePointDriveAsync();
            var targetPath = BuildSharePointPath(folderSharePoint, newFileName ?? file.Name);

            // EN: Open the local file read-only. The previous `OpenOrCreate + ReadWrite` could
            //     silently create a 0-byte source file when the path didn't exist on disk and
            //     held an exclusive write lock unnecessarily.
            // ES: Abrir el archivo local en read-only. El `OpenOrCreate + ReadWrite` anterior
            //     podía crear un archivo de 0 bytes si el path no existía y tomaba un lock
            //     exclusivo de escritura innecesario.
            using var sourceStream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

            var driveItem = await this.sharePointClient!
                .Drives[driveId]
                .Root
                .ItemWithPath(targetPath)
                .Content
                .PutAsync(sourceStream)
                ?? throw new InvalidOperationException($"SharePoint did not return a DriveItem for {targetPath}.");

            return driveItem;
        }

        public async Task<DriveItem> UploadFileSharePointAsync(IFormFile file, string? folderSharePoint = null, string? newFileName = null)
        {
            this.EnsureSharePointConfigured();

            var (_, driveId) = await this.GetSharePointDriveAsync();
            var targetPath = BuildSharePointPath(folderSharePoint, newFileName ?? file.FileName);

            using var sourceStream = file.OpenReadStream();

            var driveItem = await this.sharePointClient!
                .Drives[driveId]
                .Root
                .ItemWithPath(targetPath)
                .Content
                .PutAsync(sourceStream)
                ?? throw new InvalidOperationException($"SharePoint did not return a DriveItem for {targetPath}.");

            return driveItem;
        }

        public async Task<DriveItem> UploadFileSharePointAsync(byte[] fileContent, string fileName, string? folderSharePoint = null)
        {
            this.EnsureSharePointConfigured();

            var (_, driveId) = await this.GetSharePointDriveAsync();
            var targetPath = BuildSharePointPath(folderSharePoint, fileName);

            using var stream = new MemoryStream(fileContent);
            var driveItem = await this.sharePointClient!
                .Drives[driveId]
                .Root
                .ItemWithPath(targetPath)
                .Content
                .PutAsync(stream)
                ?? throw new InvalidOperationException($"SharePoint did not return a DriveItem for {targetPath}.");

            return driveItem;
        }

        public async Task<byte[]> DownloadFileSharePointAsync(string filePath, CancellationToken cancellationToken = default)
        {
            this.EnsureSharePointConfigured();
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            var (_, driveId) = await this.GetSharePointDriveAsync();
            var normalized = NormalizeSharePointPath(filePath);

            try
            {
                await using var stream = await this.sharePointClient!
                    .Drives[driveId]
                    .Root
                    .ItemWithPath(normalized)
                    .Content
                    .GetAsync(cancellationToken: cancellationToken);

                if (stream is null)
                {
                    throw new FileNotFoundException($"SharePoint returned no content for {normalized}.");
                }

                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken);
                return ms.ToArray();
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
            {
                // EN: Our domain exception, not System.IO's — see ElRoso.Exceptions.FileNotFoundException.
                // ES: Nuestra excepción de dominio, no la de System.IO — ver ElRoso.Exceptions.FileNotFoundException.
                throw new FileNotFoundException($"SharePoint file not found at {normalized}.", ex);
            }
        }

        public async Task DeleteFileSharePointAsync(string filePath, CancellationToken cancellationToken = default)
        {
            this.EnsureSharePointConfigured();
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            var (_, driveId) = await this.GetSharePointDriveAsync();
            var normalized = NormalizeSharePointPath(filePath);

            try
            {
                await this.sharePointClient!
                    .Drives[driveId]
                    .Root
                    .ItemWithPath(normalized)
                    .DeleteAsync(cancellationToken: cancellationToken);
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
            {
                // EN: Idempotent — already gone.
                this.logger.LogDebug(ex, "SharePoint delete: path {Path} did not exist.", normalized);
            }
        }

        private static string BuildSharePointPath(string? folder, string fileName)
        {
            var safeName = System.IO.Path.GetFileName(fileName);
            ArgumentException.ThrowIfNullOrWhiteSpace(safeName, nameof(fileName));
            return string.IsNullOrEmpty(folder) ? safeName : $"{folder}/{safeName}";
        }

        // EN: Microsoft Graph expects the path WITHOUT a leading slash relative to the Drive root.
        //     Strips a leading slash if the caller included one and normalizes backslashes.
        // ES: Graph espera el path SIN slash inicial, relativo al root del Drive. Quitamos el
        //     slash inicial si vino y normalizamos backslashes.
        private static string NormalizeSharePointPath(string filePath)
        {
            var p = filePath.Replace('\\', '/').Trim();
            return p.StartsWith('/') ? p.TrimStart('/') : p;
        }

        private void EnsureSharePointConfigured()
        {
            if (this.sharePointClient is null)
            {
                throw new InvalidOperationException(
                    "SharePoint is not configured. Verify AzureSharePoint:TenantId, ClientId, and ClientSecret in appsettings.");
            }
        }

        /// <summary>
        /// EN: Resolves the SharePoint site and drive IDs, once per instance. These IDs are stable
        ///     for the life of the site, but this used to re-resolve them on every operation: two
        ///     extra Graph round-trips per file uploaded, downloaded or deleted. The service is a
        ///     singleton, so caching here means resolving once per process.
        /// ES: Resuelve los IDs del sitio y el drive de SharePoint, una vez por instancia. Esos IDs
        ///     son estables por vida del sitio, pero antes se resolvían en cada operación: dos viajes
        ///     extra a Graph por cada archivo subido, bajado o borrado. El servicio es singleton, así
        ///     que cachear acá significa resolver una vez por proceso.
        /// </summary>
        private async Task<(string SiteId, string DriveId)> GetSharePointDriveAsync()
        {
            if (this.cachedDrive is { } alreadyResolved)
            {
                return alreadyResolved;
            }

            await this.driveGate.WaitAsync().ConfigureAwait(false);

            try
            {
                // EN: Another caller may have resolved it while we waited.
                // ES: Otro caller pudo haberlo resuelto mientras esperábamos.
                if (this.cachedDrive is { } resolvedWhileWaiting)
                {
                    return resolvedWhileWaiting;
                }

                var site = await this.sharePointClient!
                    .Sites[$"{this.configuration.GetValue<string>("AzureSharePoint:Site")}:/sites/{this.configuration.GetValue<string>("AzureSharePoint:SubSite")}"]
                    .GetAsync();

                var drive = await this.sharePointClient
                    .Sites[site!.Id!.Split(',')[1]]
                    .Drive
                    .GetAsync();

                this.cachedDrive = (site.Id!, drive!.Id!);

                this.logger.LogInformation("SharePoint drive resolved and cached for this process.");

                return this.cachedDrive.Value;
            }
            finally
            {
                this.driveGate.Release();
            }
        }
    }
}
