// <copyright file="IRepository.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Providers.NoSQL
{
    using System.Linq.Expressions;
    using ElRoso.Domains.Entities.NoSQL;
    using MongoDB.Bson;

    /// <summary>
    /// Async MongoDB repository contract. All operations are non-blocking.
    /// Contrato de repositorio MongoDB asíncrono. Todas las operaciones son no bloqueantes.
    /// </summary>
    public interface IRepository<T> : IDisposable
        where T : BaseEntity
    {
        string NameCollection { get; }

        Task InsertAsync(T entity, CancellationToken cancellationToken = default);

        Task InsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        Task<T> GetAsync(ObjectId id, CancellationToken cancellationToken = default);

        /// <summary>
        /// EN: Every document, up to the safety cap. Beyond it the result is cut and a Warning is
        ///     logged — use <see cref="GetPageAsync(int, int, CancellationToken)"/> instead.
        /// ES: Todos los documentos, hasta el tope de seguridad. Pasado el tope el resultado se corta
        ///     y se escribe un Warning — usar <see cref="GetPageAsync(int, int, CancellationToken)"/>.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IEnumerable<T>> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// EN: The documents matching the filter, up to the safety cap.
        /// ES: Los documentos que cumplen el filtro, hasta el tope de seguridad.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

        /// <summary>
        /// EN: The first document matching the filter, or <c>null</c>. Asks the server for one.
        /// ES: El primer documento que cumple el filtro, o <c>null</c>. Le pide uno al servidor.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

        /// <summary>Counts every document, on the server. / Cuenta todos los documentos, en el servidor.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<long> CountAsync(CancellationToken cancellationToken = default);

        /// <summary>Counts the documents matching the filter. / Cuenta los documentos que cumplen el filtro.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<long> CountAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

        /// <summary>
        /// EN: Whether any document matches. Asks for a single document, not for a count.
        /// ES: Si existe alguno que cumpla. Pide un solo documento, no un conteo.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<bool> AnyAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

        /// <summary>
        /// EN: A page of documents together with the total, both asked at once.
        /// ES: Una página de documentos junto con el total, pedidos a la vez.
        /// </summary>
        /// <param name="page">1-based page number. / Número de página basado en 1.</param>
        /// <param name="pageSize">Documents per page. / Documentos por página.</param>
        /// <param name="cancellationToken">Cancellation token. / Token de cancelación.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<(IReadOnlyList<T> Items, long TotalCount)> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// EN: A page of the documents matching the filter, together with the total for that filter.
        /// ES: Una página de los documentos que cumplen el filtro, con el total de ese filtro.
        /// </summary>
        /// <param name="page">1-based page number. / Número de página basado en 1.</param>
        /// <param name="pageSize">Documents per page. / Documentos por página.</param>
        /// <param name="filter">The filter. / El filtro.</param>
        /// <param name="cancellationToken">Cancellation token. / Token de cancelación.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<(IReadOnlyList<T> Items, long TotalCount)> GetPageAsync(int page, int pageSize, Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

        Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

        Task UpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

        Task DeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        Task DeleteCollectionAsync(CancellationToken cancellationToken = default);
    }
}
