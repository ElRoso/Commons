// <copyright file="AzureBlobStorageService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using Azure;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using ElRoso.Exceptions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// EN: <see cref="IBlobStorageService"/> backed by Azure Blob Storage. Connection string and
    ///     container come from <c>AzureBlob:ConnectionString</c> / <c>AzureBlob:ContainerName</c> —
    ///     read once at construction, never per call.
    /// ES: <see cref="IBlobStorageService"/> sobre Azure Blob Storage. Connection string y
    ///     container salen de <c>AzureBlob:*</c> — se leen una sola vez en el constructor, nunca
    ///     por llamada.
    /// </summary>
    internal class AzureBlobStorageService : ElRosoServiceBase, IBlobStorageService
    {
        private readonly ILogger<AzureBlobStorageService> logger;
        private readonly BlobContainerClient containerClient;
        private readonly bool createContainerIfNotExists;
        private readonly PublicAccessType containerPublicAccess;
        private readonly SemaphoreSlim containerGate = new(1, 1);
        private bool containerChecked;

        public AzureBlobStorageService(IConfiguration configuration, ILogger<AzureBlobStorageService> logger)
            : base(logger)
        {
            this.logger = logger;

            var connectionString = configuration.GetValue<string>("AzureBlob:ConnectionString")
                ?? throw new InvalidOperationException("AzureBlob:ConnectionString is required to use AzureBlobStorageService.");
            var containerName = configuration.GetValue<string>("AzureBlob:ContainerName")
                ?? throw new InvalidOperationException("AzureBlob:ContainerName is required to use AzureBlobStorageService.");

            // EN: Opt-in, default OFF. Convenient in dev (a fresh Azurite starts empty and every
            //     upload would fail with ContainerNotFound); in production the container should be
            //     created by infrastructure, so the app credentials don't need container-management
            //     rights and the access level is an explicit infra decision, not a code default.
            // ES: Opt-in, por defecto APAGADO. Cómodo en dev (un Azurite nuevo arranca vacío y toda
            //     subida fallaría con ContainerNotFound); en producción el contenedor lo crea la
            //     infraestructura, así las credenciales de la app no necesitan permisos de gestión
            //     de contenedores y el nivel de acceso es una decisión explícita de infra, no un
            //     default del código.
            this.createContainerIfNotExists = configuration.GetValue("AzureBlob:CreateContainerIfNotExists", false);

            // "blob" = los blobs se leen por URL directa pero el contenedor no se puede listar.
            // Por defecto privado: un contenedor público por accidente expone todo lo que se suba.
            this.containerPublicAccess =
                string.Equals(configuration.GetValue<string>("AzureBlob:ContainerPublicAccess"), "blob", StringComparison.OrdinalIgnoreCase)
                    ? PublicAccessType.Blob
                    : PublicAccessType.None;

            this.containerClient = new BlobContainerClient(connectionString, containerName);
        }

        public async Task<string> UploadAsync(FileInfo file, CancellationToken cancellationToken = default)
        {
            await this.EnsureContainerAsync(cancellationToken).ConfigureAwait(false);

            var blobClient = this.containerClient.GetBlobClient(file.Name);

            await using var stream = file.OpenRead();
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken).ConfigureAwait(false);

            this.logger.LogInformation("File {FileName} successfully uploaded to Azure Blob Storage.", file.Name);

            return blobClient.Uri.ToString();
        }

        public async Task<string> UploadAsync(byte[] fileContent, string fileName, CancellationToken cancellationToken = default)
        {
            await this.EnsureContainerAsync(cancellationToken).ConfigureAwait(false);

            var blobClient = this.containerClient.GetBlobClient(fileName);

            using var stream = new MemoryStream(fileContent);
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken).ConfigureAwait(false);

            this.logger.LogInformation("File {FileName} successfully uploaded to Azure Blob Storage.", fileName);

            return blobClient.Uri.ToString();
        }

        public async Task<byte[]> DownloadAsync(string fileName, CancellationToken cancellationToken = default)
        {
            var blobClient = this.containerClient.GetBlobClient(fileName);

            try
            {
                var response = await blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);

                this.logger.LogInformation("File {FileName} successfully downloaded from Azure Blob Storage.", fileName);

                return response.Value.Content.ToArray();
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // EN: Our domain exception, not System.IO's — see ElRoso.Exceptions.FileNotFoundException.
                // ES: Nuestra excepción de dominio, no la de System.IO — ver ElRoso.Exceptions.FileNotFoundException.
                throw new FileNotFoundException($"Azure Blob not found: {fileName}.", ex);
            }
        }

        public async Task DeleteAsync(string fileName, CancellationToken cancellationToken = default)
        {
            var blobClient = this.containerClient.GetBlobClient(fileName);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            this.logger.LogInformation("File {FileName} successfully deleted from Azure Blob Storage (or did not exist).", fileName);
        }

        /// <summary>
        /// EN: Creates the container on first write when opted in. Runs once per instance (the
        ///     service is a singleton) — checking on every upload would add a network round-trip to
        ///     each call for something that can only change once.
        /// ES: Crea el contenedor en la primera escritura si está habilitado. Corre una sola vez por
        ///     instancia (el servicio es singleton) — chequear en cada subida agregaría un viaje de
        ///     red por llamada para algo que solo puede cambiar una vez.
        /// </summary>
        private async Task EnsureContainerAsync(CancellationToken cancellationToken)
        {
            if (!this.createContainerIfNotExists || this.containerChecked)
            {
                return;
            }

            await this.containerGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (this.containerChecked)
                {
                    return;
                }

                await this.containerClient
                    .CreateIfNotExistsAsync(this.containerPublicAccess, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                this.containerChecked = true;

                this.logger.LogInformation(
                    "Azure Blob container {ContainerName} ensured (public access: {PublicAccess}).",
                    this.containerClient.Name,
                    this.containerPublicAccess);
            }
            finally
            {
                this.containerGate.Release();
            }
        }
    }
}
