// <copyright file="IBlobStorageService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    /// <summary>
    /// EN: Generic key/bytes object storage — S3 and Azure Blob Storage expose the same shape
    ///     (bucket/container + key + bytes in/out), so both providers implement this single
    ///     contract and a consumer picks one via DI (<c>AddS3BlobStorage</c> /
    ///     <c>AddAzureBlobStorage</c>) without depending on either SDK directly. Bucket/container,
    ///     credentials and region/account all come from configuration in the implementation's
    ///     constructor — never as method parameters, so callers stay provider-agnostic.
    /// ES: Almacenamiento de objetos clave/bytes — S3 y Azure Blob Storage tienen la misma forma
    ///     (bucket/container + key + bytes), así que ambos proveedores implementan este único
    ///     contrato y el consumidor elige uno vía DI (<c>AddS3BlobStorage</c> /
    ///     <c>AddAzureBlobStorage</c>) sin depender del SDK de ninguno de los dos. Bucket/container,
    ///     credenciales y región/cuenta salen de configuración en el constructor de la
    ///     implementación — nunca como parámetro de método, para que el caller no conozca al proveedor.
    /// </summary>
    public interface IBlobStorageService : IElRosoServiceBase
    {
        /// <summary>
        /// EN: Uploads a local file under its own file name. Returns the public URL of the object.
        /// ES: Sube un archivo local con su propio nombre. Devuelve la URL pública del objeto.
        /// </summary>
        /// <returns>The public URL of the uploaded object.</returns>
        Task<string> UploadAsync(FileInfo file, CancellationToken cancellationToken = default);

        /// <summary>
        /// EN: Uploads raw bytes under <paramref name="fileName"/>. Returns the public URL.
        /// ES: Sube bytes crudos bajo <paramref name="fileName"/>. Devuelve la URL pública.
        /// </summary>
        /// <returns>The public URL of the uploaded object.</returns>
        Task<string> UploadAsync(byte[] fileContent, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// EN: Downloads an object by its key (file name). Throws
        ///     <see cref="ElRoso.Exceptions.FileNotFoundException"/> if the key doesn't exist —
        ///     which the exception middleware answers as <b>404</b>, not 500.
        /// ES: Descarga un objeto por su key (nombre de archivo). Lanza
        ///     <see cref="ElRoso.Exceptions.FileNotFoundException"/> si la key no existe — que el
        ///     middleware de excepciones responde como <b>404</b>, no 500.
        /// </summary>
        /// <returns>The raw bytes of the downloaded object.</returns>
        Task<byte[]> DownloadAsync(string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// EN: Deletes an object by its key. Idempotent — both S3 and Azure Blob succeed even when
        ///     the key doesn't exist, so this never throws for that case.
        /// ES: Elimina un objeto por su key. Idempotente — tanto S3 como Azure Blob responden éxito
        ///     aunque la key no exista, así que nunca lanza por ese motivo.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task DeleteAsync(string fileName, CancellationToken cancellationToken = default);
    }
}
