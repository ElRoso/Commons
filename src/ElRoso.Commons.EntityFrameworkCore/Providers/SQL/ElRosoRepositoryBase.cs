// <copyright file="ElRosoRepositoryBase.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Providers.SQL
{
    using ElRoso.Commons;
    using ElRoso.Domains.Entities.SQL;
    using ElRoso.Domains.Session;
    using ElRoso.Exceptions;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    public class ElRosoRepositoryBase<TEntity> : IElRosoRepositoryBase<TEntity>
        where TEntity : BaseEntity
    {
        private const string LogMsgCacheRemove = "{Repo}: Removing data for key '{Key}' from cache.";
        private const string LogMsgDbWritten = "{Repo}: '{Count}' entries written to the database.";

        /// <summary>
        /// Default safety cap for GetAsync() without filter. / Tope de seguridad por defecto.
        /// </summary>
        private const int DefaultMaxRecordsLimit = 10_000;

        // Protected so derived classes can invalidate the cache after ExecuteUpdateAsync calls
        // that bypass the base Update/Add/Delete methods.
        // Protected para que clases derivadas puedan invalidar la caché tras llamadas
        // a ExecuteUpdateAsync que saltean los métodos base de Update/Add/Delete.
        protected readonly IMemoryCache memoryCache;

        // Exposed as protected so derived classes (e.g. UserDao) can use ExecuteUpdateAsync
        // without bypassing the base class entirely.
        // Expuesto como protected para que clases derivadas puedan usar ExecuteUpdateAsync
        // sin saltarse la clase base por completo.
        protected readonly DbContext Context;

        // Cache key and options are the same for all instances of the same TEntity type.
        // La clave y opciones de caché son iguales para todas las instancias del mismo tipo TEntity.
        private static readonly string StaticKeyNameCache = typeof(TEntity).Name;

        private static readonly MemoryCacheEntryOptions StaticCacheEntryOptions =
            new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(20))
            .SetSlidingExpiration(TimeSpan.FromMinutes(2))
            .SetPriority(CacheItemPriority.Normal);

        // One semaphore per cache key — prevents thundering herd on cold cache.
        // Un semáforo por clave — evita thundering herd cuando el caché está frío.
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> CacheLocks = new();

        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly ILogger logger;
        private readonly bool cacheIsEnabled;

        public ElRosoRepositoryBase(DbContext context, IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger logger, IMemoryCache memoryCache)
        {
            this.Context = context;
            this.httpContextAccessor = httpContextAccessor;
            this.logger = logger;
            this.memoryCache = memoryCache;

            // Read configuration once instead of on every property access.
            // Leer la configuración una sola vez en lugar de en cada acceso a la propiedad.
            this.cacheIsEnabled = configuration.GetSection("DbCacheIsEnabled").Exists()
                && configuration.GetValue<bool>("DbCacheIsEnabled");
        }

        public string KeyNameCache { get => StaticKeyNameCache; }

        public MemoryCacheEntryOptions MemoryCacheEntryOptions => StaticCacheEntryOptions;

        /// <summary>
        /// Returns a per-key SemaphoreSlim shared across all repo instances.
        /// Used to prevent thundering herd on cache misses.
        /// Devuelve un SemaphoreSlim por clave compartido entre todas las instancias del repo.
        /// </summary>
        /// <returns></returns>
        protected static SemaphoreSlim GetCacheLock(string key) => CacheLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        public CurrentUser? CurrentUser => this.GetContextItem("currentUser").HasValue() ? (CurrentUser)this.GetContextItem("currentUser")! : null;

        public bool CacheIsEnabled => this.cacheIsEnabled;

        /// <summary>
        /// Gets the safety cap for <c>GetAsync()</c> without filter — keeps a huge table from taking
        /// the process down. Override it in a derived DAO when a specific entity justifies a
        /// different ceiling.
        /// ES: Tope de seguridad para <c>GetAsync()</c> sin filtro — evita que una tabla enorme
        /// tumbe el proceso. Sobrescribirlo en un DAO derivado cuando una entidad puntual justifique
        /// otro techo.
        /// </summary>
        protected virtual int MaxRecordsLimit => DefaultMaxRecordsLimit;

        public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            var creationDate = DateTime.UtcNow;

            entity.CreationDate = creationDate;
            entity.CreatedBy = this.CurrentUser?.Id;
            entity.ModificationDate = creationDate;
            entity.ModifiedBy = this.CurrentUser?.Id;

            await this.Context.Set<TEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);

            this.DisplayStates(this.Context.ChangeTracker.Entries());

            this.logger.LogInformation(LogMsgCacheRemove, this.GetType().Name, this.KeyNameCache);
            this.memoryCache.Remove(this.KeyNameCache);

            var i = await this.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            this.logger.LogInformation(LogMsgDbWritten, this.GetType().Name, i);

            // After SaveChangesAsync, EF Core has populated all DB-generated values (NEWID, defaults)
            // on the entity object itself. A second SELECT round-trip is unnecessary.
            // Tras SaveChangesAsync, EF Core ya actualizó la entidad con los valores generados por la BD.
            // No es necesario hacer un SELECT extra.
            return entity;
        }

        public async Task<int> AddAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            var creationDate = DateTime.UtcNow;
            var currentUserId = this.CurrentUser?.Id;

            // Avoid ToList() + ForEach allocation: iterate the source directly.
            // Evitar la alocación de ToList() + ForEach: iterar directamente la fuente.
            foreach (var entity in entities)
            {
                entity.CreationDate = creationDate;
                entity.CreatedBy = currentUserId;
                entity.ModificationDate = creationDate;
                entity.ModifiedBy = currentUserId;
            }

            await this.Context.Set<TEntity>().AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);

            this.DisplayStates(this.Context.ChangeTracker.Entries());

            this.logger.LogInformation(LogMsgCacheRemove, this.GetType().Name, this.KeyNameCache);
            this.memoryCache.Remove(this.KeyNameCache);

            var i = await this.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            this.logger.LogInformation(LogMsgDbWritten, this.GetType().Name, i);
            return i;
        }

        public async Task<TEntity> AddOrUpdateSaveAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (this.Context.Entry(entity).State == EntityState.Added)
            {
                return await this.AddAsync(entity, cancellationToken).ConfigureAwait(false);
            }
            else if (this.Context.Entry(entity).State == EntityState.Modified)
            {
                return await this.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            }

            return await this.GetAsync(entity.Id, default).ConfigureAwait(false) ?? entity;
        }

        public async Task<int> AddOrUpdateSaveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            // Enumerate once and partition in a single pass to avoid iterating twice.
            // Enumerar una vez y particionar en un solo paso para evitar dos iteraciones.
            var entityList = entities.ToList();
            var toAdd = new List<TEntity>(entityList.Count);
            var toUpdate = new List<TEntity>(entityList.Count);

            foreach (var e in entityList)
            {
                var state = this.Context.Entry(e).State;
                if (state == EntityState.Added)
                {
                    toAdd.Add(e);
                }
                else if (state == EntityState.Modified)
                {
                    toUpdate.Add(e);
                }
            }

            var added = toAdd.Count > 0 ? await this.AddAsync(toAdd, cancellationToken).ConfigureAwait(false) : 0;
            var updated = toUpdate.Count > 0 ? await this.UpdateAsync(toUpdate, cancellationToken).ConfigureAwait(false) : 0;

            return added + updated;
        }

        public async Task<bool> IsExistAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await this.Context.Set<TEntity>().AnyAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> IsExistAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            return await this.IsExistAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        }

        public async Task<TEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await this.Context.Set<TEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IEnumerable<TEntity>> GetAsync(CancellationToken cancellationToken = default)
        {
            // Fast path: cache hit — no lock needed.
            // Camino rápido: hit de caché — sin lock necesario.
            if (this.memoryCache.TryGetValue(this.KeyNameCache, out List<TEntity>? valuesCache))
            {
                this.logger.LogInformation("{Repo}: Returning data for key '{Key}' from the cache.", this.GetType().Name, this.KeyNameCache);
                return valuesCache!;
            }

            // Slow path: acquire per-key lock to prevent thundering herd.
            // Camino lento: adquirir lock por clave para evitar thundering herd.
            var semaphore = GetCacheLock(this.KeyNameCache);
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Double-check: another thread may have populated the cache while we waited.
                // Double-check: otro hilo pudo haber llenado el caché mientras esperábamos.
                if (this.memoryCache.TryGetValue(this.KeyNameCache, out valuesCache))
                {
                    this.logger.LogInformation("{Repo}: Returning data for key '{Key}' from the cache (post-lock).", this.GetType().Name, this.KeyNameCache);
                    return valuesCache!;
                }

                this.logger.LogInformation("{Repo}: Cache for key '{Key}' lost. Getting from database.", this.GetType().Name, this.KeyNameCache);

                // EN: Ask for one row beyond the cap. That extra row is how we know the table had
                //     more to give — the cap used to cut in silence, so a table of 10.001 rows
                //     returned 10.000 and nobody found out.
                // ES: Se pide una fila más que el tope. Esa fila de más es cómo sabemos que la tabla
                //     tenía más para dar — el tope cortaba en silencio, así que una tabla de 10.001
                //     filas devolvía 10.000 y nadie se enteraba.
                var limit = this.MaxRecordsLimit;
                var valuesDatabase = await this.Context
                    .Set<TEntity>()
                    .AsNoTracking()
                    .Take(limit + 1)
                    .ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                var truncated = valuesDatabase.Count > limit;
                if (truncated)
                {
                    valuesDatabase.RemoveAt(valuesDatabase.Count - 1);

                    this.logger.LogWarning(
                        "{Repo}: '{Entity}' has more rows than the safety cap of {Limit} — the result was truncated. Use GetPageAsync instead.",
                        this.GetType().Name,
                        typeof(TEntity).Name,
                        limit);
                }

                // EN: A truncated result is never cached: it would serve the same incomplete answer
                //     for 20 minutes as if it were the whole table.
                // ES: Un resultado truncado nunca se cachea: serviría la misma respuesta incompleta
                //     durante 20 minutos como si fuera la tabla entera.
                if (this.CacheIsEnabled && !truncated)
                {
                    this.logger.LogInformation("{Repo}: Setting Cache for key '{Key}'.", this.GetType().Name, this.KeyNameCache);
                    this.memoryCache.Set(this.KeyNameCache, valuesDatabase, StaticCacheEntryOptions);
                }

                this.logger.LogInformation("{Repo}: Returning data for key '{Key}' from the database.", this.GetType().Name, this.KeyNameCache);
                return valuesDatabase;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<IEnumerable<TEntity>> GetAllInPaginationAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            // ORDER BY is mandatory for deterministic pagination.
            // Without it, SQL Server can return different rows on consecutive page requests.
            // ORDER BY es obligatorio para una paginación determinista.
            // Sin él, SQL Server puede devolver filas distintas en cada solicitud de página.
            return await this.Context.Set<TEntity>()
                .AsNoTracking()
                .OrderBy(e => e.CreationDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a page of results together with the total count.
        /// Both the data query and the count query run in the same EF Core round-trip context.
        /// Devuelve una página de resultados junto con el total. Ambas queries comparten el mismo contexto.
        /// </summary>
        /// <param name="page">1-based page number. / Número de página basado en 1.</param>
        /// <param name="pageSize">Number of records per page. / Número de registros por página.</param>
        /// <param name="cancellationToken">Cancellation token. / Token de cancelación.</param>
        /// <returns>Tuple with the items for the requested page and the total record count.</returns>
        public async Task<(IReadOnlyList<TEntity> Items, int TotalCount)> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var baseQuery = this.Context.Set<TEntity>().AsNoTracking();

            // Execute count and data in parallel — two SQL commands, same DbContext.
            // Ejecutar count y datos en paralelo — dos comandos SQL, mismo DbContext.
            var countTask = baseQuery.CountAsync(cancellationToken);
            var itemsTask = baseQuery
                .OrderBy(e => e.CreationDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            await Task.WhenAll(countTask, itemsTask).ConfigureAwait(false);

            return (itemsTask.Result, countTask.Result);
        }

        public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            var modificationDate = DateTime.UtcNow;

            // Fetch only the two audit fields needed — avoids pulling the full row.
            // Solo traemos los dos campos de auditoría necesarios — evita traer toda la fila.
            var audit = await this.Context.Set<TEntity>()
                .AsNoTracking()
                .Where(e => e.Id == entity.Id)
                .Select(e => new { e.CreatedBy, e.CreationDate })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            // Guard: entity must exist before attempting an update.
            // Guard: la entidad debe existir antes de intentar una actualización.
            if (audit is null)
            {
                throw new NotFoundException($"Entity '{typeof(TEntity).Name}' with Id '{entity.Id}' was not found. / Entidad '{typeof(TEntity).Name}' con Id '{entity.Id}' no encontrada.");
            }

            entity.CreatedBy = audit.CreatedBy;
            entity.CreationDate = audit.CreationDate;
            entity.ModificationDate = modificationDate;
            entity.ModifiedBy = this.CurrentUser?.Id;

            this.DetachExistingEntry(entity.Id);
            this.Context.Set<TEntity>().Update(entity);

            this.DisplayStates(this.Context.ChangeTracker.Entries());

            this.logger.LogInformation(LogMsgCacheRemove, this.GetType().Name, this.KeyNameCache);
            this.memoryCache.Remove(this.KeyNameCache);

            var i = await this.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            this.logger.LogInformation(LogMsgDbWritten, this.GetType().Name, i);

            // Return the entity directly — it already has all updated field values set above.
            // No se necesita un SELECT extra; la entidad ya tiene todos los valores correctos.
            return entity;
        }

        public async Task<int> UpdateAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            var modificationDate = DateTime.UtcNow;
            var currentUserId = this.CurrentUser?.Id;
            var entityList = entities.ToList();

            // Fetch all originals in ONE query instead of N individual SELECTs.
            // Traer todos los originales en UNA sola query en lugar de N SELECTs individuales.
            var ids = entityList.Select(e => e.Id).ToList();
            var originals = await this.Context.Set<TEntity>()
                .AsNoTracking()
                .Where(e => ids.Contains(e.Id))
                .Select(e => new { e.Id, e.CreatedBy, e.CreationDate })
                .ToDictionaryAsync(e => e.Id, cancellationToken)
                .ConfigureAwait(false);

            foreach (var entity in entityList)
            {
                if (!originals.TryGetValue(entity.Id, out var original))
                {
                    throw new NotFoundException($"Entity '{typeof(TEntity).Name}' with Id '{entity.Id}' was not found. / Entidad no encontrada.");
                }

                entity.CreatedBy = original.CreatedBy;
                entity.CreationDate = original.CreationDate;
                entity.ModificationDate = modificationDate;
                entity.ModifiedBy = currentUserId;

                this.DetachExistingEntry(entity.Id);
            }

            this.Context.Set<TEntity>().UpdateRange(entityList);

            this.DisplayStates(this.Context.ChangeTracker.Entries());

            this.logger.LogInformation(LogMsgCacheRemove, this.GetType().Name, this.KeyNameCache);
            this.memoryCache.Remove(this.KeyNameCache);

            var i = await this.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            this.logger.LogInformation(LogMsgDbWritten, this.GetType().Name, i);
            return i;
        }

        public async Task<int> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            this.DetachExistingEntry(entity.Id);
            this.Context.Set<TEntity>().Remove(entity);

            this.DisplayStates(this.Context.ChangeTracker.Entries());

            this.logger.LogInformation(LogMsgCacheRemove, this.GetType().Name, this.KeyNameCache);
            this.memoryCache.Remove(this.KeyNameCache);

            var i = await this.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            this.logger.LogInformation(LogMsgDbWritten, this.GetType().Name, i);
            return i;
        }

        public async Task<int> DeleteAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            foreach (var entity in entities)
            {
                this.DetachExistingEntry(entity.Id);
            }

            this.Context.Set<TEntity>().RemoveRange(entities);

            this.DisplayStates(this.Context.ChangeTracker.Entries());

            this.logger.LogInformation(LogMsgCacheRemove, this.GetType().Name, this.KeyNameCache);
            this.memoryCache.Remove(this.KeyNameCache);

            var i = await this.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            this.logger.LogInformation(LogMsgDbWritten, this.GetType().Name, i);
            return i;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public object? GetContextItem(string item) => this.httpContextAccessor.HttpContext?.Items[item];

        /// <summary>
        /// EN: Deliberately does NOT dispose the <see cref="DbContext"/>. The repository did not
        ///     create it — the DI container did, and the container disposes it at the end of the
        ///     scope. Disposing it here meant the first repository disposed in a request left every
        ///     other DAO of that same request holding a dead context, since they all share the same
        ///     scoped instance.
        /// ES: A propósito NO dispone el <see cref="DbContext"/>. El repositorio no lo creó — lo
        ///     creó el contenedor de DI, y el contenedor lo dispone al cerrar el scope. Disponerlo
        ///     acá hacía que el primer repositorio dispuesto de un request dejara a todos los demás
        ///     DAOs de ese request con un contexto muerto, porque comparten la misma instancia
        ///     scoped.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            // EN: Nothing to release: the repository owns no unmanaged resource of its own. The
            //     method stays because IElRosoRepositoryBase is IDisposable and derived DAOs may
            //     hold something that does need releasing.
            // ES: Nada que liberar: el repositorio no es dueño de ningún recurso propio. El método
            //     queda porque IElRosoRepositoryBase es IDisposable y un DAO derivado puede tener
            //     algo que sí haya que liberar.
        }

        private void DetachExistingEntry(Guid id)
        {
            var existing = this.Context.ChangeTracker.Entries<TEntity>().FirstOrDefault(e => e.Entity.Id == id);
            if (existing != null)
            {
                existing.State = EntityState.Detached;
            }
        }

        private void DisplayStates(IEnumerable<EntityEntry> entries)
        {
            // Guard against argument evaluation when Debug logging is disabled.
            // Evitar la evaluación de argumentos cuando el nivel Debug no está activo.
            if (!this.logger.IsEnabled(LogLevel.Debug))
            {
                return;
            }

            foreach (var entry in entries)
            {
                this.logger.LogDebug("Entity: {Entity}, State: {State}", entry.Entity.GetType().Name, entry.State);
            }
        }
    }
}
