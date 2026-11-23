// <copyright file="S3BlobStorageService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using Amazon;
    using Amazon.S3;
    using Amazon.S3.Transfer;
    using ElRoso.Exceptions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// EN: <see cref="IBlobStorageService"/> backed by AWS S3. Bucket, credentials and region come
    ///     from <c>AWS:BucketName</c> / <c>AWS:AccessKeyId</c> / <c>AWS:SecretAccessKey</c> /
    ///     <c>AWS:Region</c> — read once at construction, never per call.
    ///     Optional <c>AWS:ServiceURL</c> points the client at an S3-COMPATIBLE endpoint that is not
    ///     AWS and switches both the client and the returned urls to path-style addressing.
    /// ES: <see cref="IBlobStorageService"/> sobre AWS S3. Bucket, credenciales y región salen de
    ///     <c>AWS:*</c> — se leen una sola vez en el constructor, nunca por llamada.
    ///     El opcional <c>AWS:ServiceURL</c> apunta el cliente a un endpoint S3-COMPATIBLE que no es
    ///     AWS y pasa tanto el cliente como las urls devueltas a direccionamiento path-style.
    /// </summary>
    internal class S3BlobStorageService : ElRosoServiceBase, IBlobStorageService
    {
        private readonly ILogger<S3BlobStorageService> logger;
        private readonly string bucketName;
        private readonly RegionEndpoint region;

        // EN: Set only when pointing at an S3-COMPATIBLE endpoint that is not AWS (Cloudflare R2,
        //     MinIO). Null means plain AWS, which is what every existing caller gets.
        // ES: Se setea solo cuando se apunta a un endpoint S3-COMPATIBLE que no es AWS (Cloudflare
        //     R2, MinIO). Null significa AWS a secas, que es lo que recibe todo caller actual.
        private readonly string? serviceUrl;

        // EN: The AWS client owns an HTTP handler and its connection pool, so it is built once and
        //     reused — it is thread-safe and that is what AWS recommends. Building one per call
        //     threw the pool away on every operation, in a service DI registers as a singleton
        //     precisely so the expensive parts get built once.
        // ES: El cliente de AWS es dueño de un handler HTTP y su pool de conexiones, así que se
        //     arma una vez y se reutiliza — es thread-safe y es lo que AWS recomienda. Armar uno por
        //     llamada tiraba el pool en cada operación, en un servicio que DI registra como
        //     singleton justamente para que lo caro se arme una sola vez.
        private readonly IAmazonS3 client;

        public S3BlobStorageService(IConfiguration configuration, ILogger<S3BlobStorageService> logger)
            : base(logger)
        {
            this.logger = logger;
            var accessKeyId = configuration.GetValue<string>("AWS:AccessKeyId") ?? string.Empty;
            var secretAccessKey = configuration.GetValue<string>("AWS:SecretAccessKey") ?? string.Empty;
            this.bucketName = configuration.GetValue<string>("AWS:BucketName") ?? string.Empty;

            var regionName = configuration.GetValue<string>("AWS:Region")
                ?? throw new InvalidOperationException("AWS:Region is required to use S3BlobStorageService.");
            this.region = RegionEndpoint.GetBySystemName(regionName);

            // EN: An S3-COMPATIBLE endpoint that is not AWS. Two settings travel together and
            //     neither works without the other:
            //     · ServiceURL points the client somewhere other than amazonaws.com.
            //     · ForcePathStyle switches the url layout to {endpoint}/{bucket}/{key}. Without it
            //       the SDK builds virtual-hosted urls ({bucket}.{endpoint}/{key}) that these
            //       providers do not resolve — and the failure is a DNS error at call time, not a
            //       configuration error at startup, so a half-configured client looks healthy.
            //     ⚠️ The region still has to be read: it is what signs the request. R2 documents the
            //     literal "auto", which is not an AWS region — GetBySystemName accepts unknown names
            //     and returns a synthetic endpoint, and with ServiceURL set that endpoint is never
            //     used to route, only to sign.
            // ES: Un endpoint S3-COMPATIBLE que no es AWS. Dos settings viajan juntos y ninguno
            //     sirve sin el otro:
            //     · ServiceURL apunta el cliente a otro lado que no sea amazonaws.com.
            //     · ForcePathStyle cambia el formato de url a {endpoint}/{bucket}/{key}. Sin eso el
            //       SDK arma urls virtual-hosted ({bucket}.{endpoint}/{key}) que estos proveedores
            //       no resuelven — y la falla es un error de DNS al llamar, no un error de
            //       configuración al arrancar, así que un cliente a medio configurar parece sano.
            //     ⚠️ La región se sigue leyendo: es lo que firma el request. R2 documenta el literal
            //     "auto", que no es una región de AWS — GetBySystemName acepta nombres desconocidos
            //     y devuelve un endpoint sintético, y con ServiceURL seteado ese endpoint nunca
            //     rutea, solo firma.
            this.serviceUrl = configuration.GetValue<string>("AWS:ServiceURL");

            this.client = string.IsNullOrWhiteSpace(this.serviceUrl)
                ? new AmazonS3Client(accessKeyId, secretAccessKey, this.region)
                : new AmazonS3Client(accessKeyId, secretAccessKey, new AmazonS3Config
                {
                    ServiceURL = this.serviceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = regionName,
                });
        }

        public async Task<string> UploadAsync(FileInfo file, CancellationToken cancellationToken = default)
        {
            var fileTransferUtility = new TransferUtility(this.client);
            await fileTransferUtility.UploadAsync(file.FullName, this.bucketName, file.Name, cancellationToken);

            this.logger.LogInformation("File {FileName} successfully uploaded to S3.", file.Name);

            return this.BuildUrl(file.Name);
        }

        public async Task<string> UploadAsync(byte[] fileContent, string fileName, CancellationToken cancellationToken = default)
        {
            var fileTransferUtility = new TransferUtility(this.client);

            using var stream = new MemoryStream(fileContent);
            await fileTransferUtility.UploadAsync(stream, this.bucketName, fileName, cancellationToken);

            this.logger.LogInformation("File {FileName} successfully uploaded to S3.", fileName);

            return this.BuildUrl(fileName);
        }

        public async Task<byte[]> DownloadAsync(string fileName, CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await this.client.GetObjectAsync(this.bucketName, fileName, cancellationToken).ConfigureAwait(false);
                using var ms = new MemoryStream();
                await response.ResponseStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);

                this.logger.LogInformation("File {FileName} successfully downloaded from S3.", fileName);

                return ms.ToArray();
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // EN: Our domain exception, not System.IO's — that one is not recognised as a
                //     domain error and came back as a 500 with the message masked in production.
                // ES: Nuestra excepción de dominio, no la de System.IO — esa no se reconoce como
                //     error de dominio y volvía como 500 con el mensaje enmascarado en producción.
                throw new FileNotFoundException($"S3 object not found: {fileName}.", ex);
            }
        }

        public async Task DeleteAsync(string fileName, CancellationToken cancellationToken = default)
        {
            await this.client.DeleteObjectAsync(this.bucketName, fileName, cancellationToken).ConfigureAwait(false);

            this.logger.LogInformation("File {FileName} successfully deleted from S3 (or did not exist).", fileName);
        }

        private string BuildUrl(string fileName) => BuildUrl(this.bucketName, this.region, fileName, this.serviceUrl);

        // EN: Virtual-hosted-style URL (https://{bucket}.s3.{region}.amazonaws.com/{key}) — the format
        //     AWS recommends over the deprecated path-style. Public only if the bucket/object policy
        //     allows it; callers needing a private/expiring link should use a presigned URL instead
        //     (not implemented here — none of the current call sites need it).
        // ES: URL virtual-hosted-style — el formato que AWS recomienda sobre el path-style deprecado.
        //     Pública solo si la policy del bucket/objeto lo permite; si se necesita un link privado o
        //     con expiración, usar una presigned URL (no implementada acá — ningún caller la necesita hoy).
        internal static string BuildUrl(string bucketName, RegionEndpoint regionEndpoint, string fileName, string? serviceUrl = null)
        {
            // EN: Escape each path segment on its own — Uri.EscapeDataString escapes '/' too, which
            //     would corrupt a "folder-like" S3 key (e.g. "avatars/user123.png") into a single
            //     opaque segment instead of a path.
            // ES: Se escapa cada segmento del path por separado — Uri.EscapeDataString también escapa
            //     '/', lo que rompería una key S3 "tipo carpeta" (ej. "avatars/user123.png").
            var escapedSegments = fileName.Split('/').Select(Uri.EscapeDataString);
            var key = string.Join('/', escapedSegments);

            // EN: 🔴 The url MUST match the style the client is actually speaking. With a
            //     ServiceURL the client was built with ForcePathStyle, so handing back a
            //     virtual-hosted amazonaws.com url would be a link to nothing — and callers persist
            //     what this returns, so the wrong url outlives the request that produced it.
            // ES: 🔴 La url TIENE que coincidir con el estilo que el cliente está hablando. Con un
            //     ServiceURL el cliente se armó con ForcePathStyle, así que devolver una url
            //     virtual-hosted de amazonaws.com sería un link a la nada — y los callers persisten
            //     lo que esto devuelve, así que la url equivocada sobrevive al request que la creó.
            if (!string.IsNullOrWhiteSpace(serviceUrl))
            {
                return $"{serviceUrl.TrimEnd('/')}/{bucketName}/{key}";
            }

            return $"https://{bucketName}.s3.{regionEndpoint.SystemName}.amazonaws.com/{key}";
        }
    }
}
