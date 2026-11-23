// <copyright file="ISharePointFileService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Graph.Models;

    /// <summary>
    /// EN: SharePoint file access via Microsoft Graph — a fundamentally different shape than plain
    ///     object storage (site/drive/folder navigation, <see cref="DriveItem"/> results), so it's
    ///     its own contract instead of forcing it into <see cref="IBlobStorageService"/>.
    /// ES: Acceso a archivos de SharePoint vía Microsoft Graph — una forma fundamentalmente
    ///     distinta al almacenamiento de objetos plano (navegación site/drive/carpeta, resultados
    ///     <see cref="DriveItem"/>), por eso es un contrato propio en vez de forzarlo dentro de
    ///     <see cref="IBlobStorageService"/>.
    /// </summary>
    public interface ISharePointFileService : IElRosoServiceBase
    {
        Task<DriveItem> UploadFileSharePointAsync(FileInfo file, string? folderSharePoint = null, string? newFileName = null);

        Task<DriveItem> UploadFileSharePointAsync(IFormFile file, string? folderSharePoint = null, string? newFileName = null);

        Task<DriveItem> UploadFileSharePointAsync(byte[] fileContent, string fileName, string? folderSharePoint = null);

        /// <summary>
        /// EN: Downloads a file from SharePoint by its full path inside the configured Drive
        ///     (e.g. <c>Comprobantes/2026/&lt;CompanyId&gt;/&lt;file.pdf&gt;</c>). Returns the raw bytes.
        ///     Throws <see cref="ElRoso.Exceptions.FileNotFoundException"/> (a 404) if the path
        ///     doesn't exist.
        /// ES: Descarga un archivo de SharePoint por su path completo dentro del Drive
        ///     configurado. Devuelve los bytes crudos. Lanza
        ///     <see cref="ElRoso.Exceptions.FileNotFoundException"/> (un 404) si no existe.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<byte[]> DownloadFileSharePointAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// EN: Deletes a file from SharePoint by its full path. Idempotent — returns silently if
        ///     the path doesn't exist.
        /// ES: Elimina un archivo de SharePoint por su path completo. Idempotente — si no existe
        ///     no hace nada.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task DeleteFileSharePointAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
