// <copyright file="Repository.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Providers.NoSQL
{
    using System.Linq.Expressions;
    using ElRoso.Commons;
    using ElRoso.Domains.Entities.NoSQL;
    using ElRoso.Domains.Session;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using MongoDB.Bson;
    using MongoDB.Driver;

    /// <summary>
    /// Base MongoDB repository with fully async operations and proper bulk support.
    /// Repositorio MongoDB base con operaciones completamente asíncronas y soporte de operaciones bulk.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: Register <see cref="IMongoDatabase"/> as a Singleton in DI.
    /// <see cref="MongoClient"/> is thread-safe and manages its own connection pool —
    /// creating one per repository instance destroys pooling and adds significant startup cost.
    ///
    /// IMPORTANTE: Registrar <see cref="IMongoDatabase"/> como Singleton en DI.
    /// <see cref="MongoClient"/> es thread-safe y gestiona su propio pool de conexiones.
    /// Crear uno por instancia destruye el pooling y agrega costo de startup significativo.
    /// </remarks>
    public abstract class Repository<T> : IRepository<T>
        where T : BaseEntity
    {
        private const int DefaultMaxRecordsLimit = 10_000;

        private readonly ILogger<Repository<T>> logger;
        private readonly IMongoDatabase mongoDatabase;
        private readonly IHttpContextAccessor? httpContextAccessor;

        public abstract string NameCollection { get; }

        /// <summary>
        /// Gets the ceiling for the methods that return "everything". Override it in a derived
        /// repository whose collection legitimately holds more. Mirrors the SQL repository's cap.
        /// ES: Techo de los métodos que devuelven "todo". Se sobrescribe en un repositorio derivado
        ///     cuya colección legítimamente tenga más. Es el mismo tope que el repositorio de SQL.
        /// </summary>
        protected virtual int MaxRecordsLimit => DefaultMaxRecordsLimit;

        /// <summary>
        /// Gets the authenticated user, or <c>null</c> when there is no request in flight (a cron
        /// job, a background service) or the accessor was not provided.
        /// ES: El usuario autenticado, o <c>null</c> cuando no hay request en curso (un cron job, un
        /// servicio en segundo plano) o no se pasó el accessor.
        /// </summary>
        protected CurrentUser? CurrentUser =>
            this.httpContextAccessor?.HttpContext?.Items["currentUser"] as CurrentUser;

        /// <param name="mongoDatabase">
        /// Must be registered as Singleton. / Debe estar registrado como Singleton.
        /// </param>
        /// <param name="logger">Logger for the repository. / Logger del repositorio.</param>
        protected Repository(IMongoDatabase mongoDatabase, ILogger<Repository<T>> logger)
            : this(mongoDatabase, logger, httpContextAccessor: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Repository{T}"/> class.
        /// EN: Overload that records WHO wrote each document. Without the accessor only the dates
        ///     are stamped, which is how this repository behaved until 2026-08-05: the Audit object
        ///     had CreatedBy and ModifiedBy, so documents looked audited, and they were always null.
        /// ES: Sobrecarga que registra QUIÉN escribió cada documento. Sin el accessor solo se
        ///     estampan las fechas, que es como se comportaba este repositorio hasta el 2026-08-05:
        ///     el objeto Audit tenía CreatedBy y ModifiedBy, así que los documentos parecían
        ///     auditados, y siempre estaban en null.
        /// </summary>
        /// <param name="mongoDatabase">Must be registered as Singleton. / Debe estar registrado como Singleton.</param>
        /// <param name="logger">Logger for the repository. / Logger del repositorio.</param>
        /// <param name="httpContextAccessor">Resolves the current user. / Resuelve el usuario actual.</param>
        protected Repository(IMongoDatabase mongoDatabase, ILogger<Repository<T>> logger, IHttpContextAccessor? httpContextAccessor)
        {
            this.mongoDatabase = mongoDatabase;
            this.logger = logger;
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <summary>Inserts a single document asynchronously. / Inserta un documento de forma asíncrona.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task InsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            var collection = this.mongoDatabase.GetCollection<T>(this.NameCollection);
            var currentUserId = this.CurrentUser?.Id;

            entity.Audit.CreationDate = DateTime.UtcNow;
            entity.Audit.ModificationDate = entity.Audit.CreationDate;
            entity.Audit.CreatedBy = currentUserId;
            entity.Audit.ModifiedBy = currentUserId;

            await collection.InsertOneAsync(entity, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Inserts multiple documents in a single bulk operation. / Inserta múltiples documentos en una sola operación bulk.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task InsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            var collection = this.mongoDatabase.GetCollection<T>(this.NameCollection);
            var now = DateTime.UtcNow;
            var currentUserId = this.CurrentUser?.Id;

            foreach (var entity in entities)
            {
                entity.Audit.CreationDate = now;
                entity.Audit.ModificationDate = now;
                entity.Audit.CreatedBy = currentUserId;
                entity.Audit.ModifiedBy = currentUserId;
            }

            await collection.InsertManyAsync(entities, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Finds a document by its ObjectId. / Busca un documento por su ObjectId.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<T> GetAsync(ObjectId id, CancellationToken cancellationToken = default)
        {
            var collection = this.mongoDatabase.GetCollection<T>(this.NameCollection);
            var cursor = await collection.FindAsync(x => x.Id == id, cancellationToken: cancellationToken).ConfigureAwait(false);
            return await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// EN: Every document, up to <see cref="MaxRecordsLimit"/>. Beyond it the result is cut and a
        ///     Warning is logged — until 2026-08-05 this method had no cap at all and brought the
        ///     whole collection into memory, while the SQL side had stopped at 10.000 rows for years.
        /// ES: Todos los documentos, hasta <see cref="MaxRecordsLimit"/>. Pasado el tope el resultado
        ///     se corta y se escribe un Warning — hasta el 2026-08-05 este método no tenía ningún tope
        ///     y se traía la colección entera a memoria, mientras que el lado SQL cortaba en 10.000
        ///     filas desde hacía años.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IEnumerable<T>> GetAsync(CancellationToken cancellationToken = default)
            => await this.FindWithCapAsync(FilterDefinition<T>.Empty, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// EN: The documents matching the filter, up to <see cref="MaxRecordsLimit"/>. Before this
        ///     existed the only way to query by anything other than the id was to pull the whole
        ///     collection and filter in memory.
        /// ES: Los documentos que cumplen el filtro, hasta <see cref="MaxRecordsLimit"/>. Antes de que
        ///     esto existiera, la única forma de consultar por algo que no fuera el id era traerse la
        ///     colección entera y filtrar en memoria.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
            => await this.FindWithCapAsync(Builders<T>.Filter.Where(filter), cancellationToken).ConfigureAwait(false);

        /// <summary>Returns the first match, or <c>null</c>. / Devuelve la primera coincidencia, o <c>null</c>.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        {
            var collection = this.mongoDatabase.GetCollection<T>(this.NameCollection);
            var options = new FindOptions<T, T> { Limit = 1 };

            var cursor = await collection
                .FindAsync(Builders<T>.Filter.Where(filter), options, cancellationToken)
                .ConfigureAwait(false);

            return await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Counts every document, on the server. / Cuenta todos los documentos, en el servidor.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<long> CountAsync(CancellationToken cancellationToken = default)
            => await this.CountAsync(FilterDefinition<T>.Empty, cancellationToken).ConfigureAwait(false);

        /// <summary>Counts the documents matching the filter. / Cuenta los que cumplen el filtro.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<long> CountAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
            => await this.CountAsync(Builders<T>.Filter.Where(filter), cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// EN: Whether any document matches. Asks for one document, not for a count: the server can
        ///     stop at the first hit instead of walking every match.
        /// ES: Si existe alguno que cumpla. Pide un documento, no un conteo: el servidor puede parar
        ///     en la primera coincidencia en vez de recorrerlas todas.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<bool> AnyAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
            => await this.FirstOrDefaultAsync(filter, cancellationToken).ConfigureAwait(false) is not null;

        /// <summary>
        /// EN: A page of documents together with the total. Both go out at the same time, so the page
        ///     costs one round trip instead of two.
        /// ES: Una página de documentos junto con el total. Los dos salen a la vez, así que la página
        ///     cuesta un viaje en vez de dos.
        /// </summary>
        /// <param name="page">1-based page number. / Número de página basado en 1.</param>
        /// <param name="pageSize">Documents per page. / Documentos por página.</param>
        /// <param name="cancellationToken">Cancellation token. / Token de cancelación.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<(IReadOnlyList<T> Items, long TotalCount)> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => await this.GetPageAsync(page, pageSize, FilterDefinition<T>.Empty, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// EN: A page of the documents matching the filter, with the total for that same filter.
        /// ES: Una página de los documentos que cumplen el filtro, con el total de ese mismo filtro.
        /// </summary>
        /// <param name="page">1-based page number. / Número de página basado en 1.</param>
        /// <param name="pageSize">Documents per page. / Documentos por página.</param>
        /// <param name="filter">The filter. / El filtro.</param>
        /// <param name="cancellationToken">Cancellation token. / Token de cancelación.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<(IReadOnlyList<T> Items, long TotalCount)> GetPageAsync(int page, int pageSize, Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
            => await this.GetPageAsync(page, pageSize, Builders<T>.Filter.Where(filter), cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Replaces a document in-place using ReplaceOneAsync — atomic, single round trip.
        /// No longer implemented as Delete + Insert which was non-atomic and required 2 round trips.
        ///
        /// Reemplaza un documento en su lugar con ReplaceOneAsync — atómico, un solo round trip.
        /// Ya no se implementa como Delete + Insert, que era no atómico y requería 2 round trips.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            var collection = this.mongoDatabase.GetCollection<T>(this.NameCollection);

            // EN: Only the modification half is touched — rewriting CreatedBy would erase the half
            //     of the trail that cannot be reconstructed afterwards.
            // ES: Solo se toca la mitad de modificación — reescribir CreatedBy borraría la mitad de
            //     la traza que no se puede reconstruir después.
            entity.Audit.ModificationDate = DateTime.UtcNow;
            entity.Audit.ModifiedBy = this.CurrentUser?.Id;

            await collection
                .ReplaceOneAsync(x => x.Id == entity.Id, entity, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Replaces multiple documents in a single BulkWrite — one round trip regardless of count.
        /// The previous implementation issued N sequential ReplaceOneAsync calls, so the cost grew
        /// with the network latency times the number of documents.
        ///
        /// Reemplaza múltiples documentos en un solo BulkWrite — un round trip sin importar la
        /// cantidad. La implementación anterior hacía N llamadas secuenciales a ReplaceOneAsync, con
        /// lo que el costo crecía con la latencia de red multiplicada por la cantidad de documentos.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task UpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var currentUserId = this.CurrentUser?.Id;
            var writes = new List<WriteModel<T>>();

            foreach (var entity in entities)
            {
                entity.Audit.ModificationDate = now;
                entity.Audit.ModifiedBy = currentUserId;

                writes.Add(new ReplaceOneModel<T>(
                    Builders<T>.Filter.Eq(x => x.Id, entity.Id),
                    entity));
            }

            // EN: BulkWriteAsync rejects an empty request list; the old loop just did nothing.
            // ES: BulkWriteAsync rechaza la lista vacía; el loop viejo simplemente no hacía nada.
            if (writes.Count == 0)
            {
                return;
            }

            var collection = this.mongoDatabase.GetCollection<T>(this.NameCollection);

            // EN: Ordered on purpose — the sequential loop stopped at the first failure, and an
            //     unordered bulk would instead keep writing past it.
            // ES: Ordenado a propósito — el loop secuencial se detenía en la primera falla, y un
            //     bulk desordenado seguiría escribiendo después de ella.
            await collection
                .BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = true }, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>Deletes a single document by Id. / Elimina un documento por Id.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            var collection = this.mongoDatabase.GetCollection<T>(this.NameCollection);
            await collection
                .DeleteOneAsync(x => x.Id == entity.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes multiple documents in a single DeleteMany operation — one round trip regardless of count.
        /// Previous implementation made N individual DeleteOne calls.
        ///
        /// Elimina múltiples documentos en una sola operación DeleteMany — un round trip sin importar la cantidad.
        /// La implementación anterior hacía N llamadas individuales a DeleteOne.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task DeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            var ids = entities.Select(e => e.Id).ToList();
            if (ids.Count == 0)
            {
                return;
            }

            var collection = this.mongoDatabase.GetCollection<T>(this.NameCollection);
            await collection
                .DeleteManyAsync(x => ids.Contains(x.Id), cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>Drops the entire collection. / Elimina toda la colección.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
            => await this.mongoDatabase.DropCollectionAsync(this.NameCollection, cancellationToken).ConfigureAwait(false);

        private async Task<long> CountAsync(FilterDefinition<T> filter, CancellationToken cancellationToken)
        {
            var collection = this.mongoDatabase.GetCollection<T>(this.NameCollection);
            return await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private async Task<(IReadOnlyList<T> Items, long TotalCount)> GetPageAsync(int page, int pageSize, FilterDefinition<T> filter, CancellationToken cancellationToken)
        {
            // EN: Page 0 would compute a negative Skip; failing here says which argument was wrong,
            //     instead of surfacing as a driver error far from the caller.
            // ES: La página 0 daría un Skip negativo; fallar acá dice qué argumento estaba mal, en vez
            //     de aparecer como un error del driver lejos de quien llamó.
            ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

            var collection = this.mongoDatabase.GetCollection<T>(this.NameCollection);

            // EN: Sorting is what makes paging deterministic — without it the server may return the
            //     same document on two consecutive pages and skip another one entirely.
            //     ⚠️ Needs an index on Audit.CreationDate: an unindexed sort is done in memory and
            //     Mongo aborts it past 32 MB.
            // ES: El orden es lo que hace determinista la paginación — sin él el servidor puede
            //     devolver el mismo documento en dos páginas seguidas y saltearse otro.
            //     ⚠️ Necesita un índice sobre Audit.CreationDate: un orden sin índice se hace en
            //     memoria y Mongo lo aborta pasados los 32 MB.
            var options = new FindOptions<T, T>
            {
                Skip = (page - 1) * pageSize,
                Limit = pageSize,
                Sort = Builders<T>.Sort.Ascending(x => x.Audit.CreationDate),
            };

            var countTask = collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var itemsTask = collection.FindAsync(filter, options, cancellationToken);

            await Task.WhenAll(countTask, itemsTask).ConfigureAwait(false);

            var items = await itemsTask.Result.ToListAsync(cancellationToken).ConfigureAwait(false);

            return (items, countTask.Result);
        }

        private async Task<IReadOnlyList<T>> FindWithCapAsync(FilterDefinition<T> filter, CancellationToken cancellationToken)
        {
            // EN: Ask for one document beyond the cap. That extra one is how we know the collection
            //     had more to give — a cap that cuts in silence turns a partial answer into what
            //     looks like the whole thing.
            // ES: Se pide un documento más que el tope. Ese de más es cómo sabemos que la colección
            //     tenía más para dar — un tope que corta en silencio convierte una respuesta parcial
            //     en lo que parece la completa.
            var limit = this.MaxRecordsLimit;
            var collection = this.mongoDatabase.GetCollection<T>(this.NameCollection);
            var options = new FindOptions<T, T> { Limit = limit + 1 };

            var cursor = await collection.FindAsync(filter, options, cancellationToken).ConfigureAwait(false);
            var documents = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);

            if (documents.Count > limit)
            {
                documents.RemoveAt(documents.Count - 1);

                this.logger.LogWarning(
                    "{Repo}: '{Collection}' has more documents than the safety cap of {Limit} — the result was truncated. Use GetPageAsync instead.",
                    this.GetType().Name,
                    this.NameCollection,
                    limit);
            }

            return documents;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
