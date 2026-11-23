// <copyright file="IElRosoRepositoryBase.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Providers.SQL
{
    using ElRoso.Domains.Entities.SQL;
    using ElRoso.Domains.Session;
    using Microsoft.Extensions.Caching.Memory;
    using System;
    using System.Collections.Generic;

    public interface IElRosoRepositoryBase<TEntity> : IDisposable
        where TEntity : BaseEntity
    {
        string KeyNameCache { get; }

        bool CacheIsEnabled { get; }

        MemoryCacheEntryOptions MemoryCacheEntryOptions { get; }

        CurrentUser? CurrentUser { get; }

        Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

        Task<int> AddAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        Task<bool> IsExistAsync(Guid id, CancellationToken cancellationToken = default);

        Task<bool> IsExistAsync(TEntity entity, CancellationToken cancellationToken = default);

        Task<TEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        Task<IEnumerable<TEntity>> GetAsync(CancellationToken cancellationToken = default);

        Task<IEnumerable<TEntity>> GetAllInPaginationAsync(int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a paginated page together with the total record count in a single query pass.
        /// Devuelve una página paginada junto con el total de registros en un solo recorrido de consulta.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<(IReadOnlyList<TEntity> Items, int TotalCount)> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);

        Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

        Task<int> UpdateAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        Task<int> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

        Task<int> DeleteAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        Task<TEntity> AddOrUpdateSaveAsync(TEntity entity, CancellationToken cancellationToken = default);

        Task<int> AddOrUpdateSaveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        object? GetContextItem(string item);
    }
}
