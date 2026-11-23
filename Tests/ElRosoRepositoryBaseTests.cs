namespace ElRoso.Commons.Tests;

using ElRoso.Domains.Entities.SQL;
using ElRoso.Exceptions;
using ElRoso.Providers.SQL;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

// Minimal entity for repository tests — no domain coupling.
// Entidad mínima para tests del repositorio — sin acoplamiento de dominio.
internal class TestEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// Minimal DbContext for in-memory testing.
// DbContext mínimo para testing en memoria.
internal class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions options) : base(options) { }

    public DbSet<TestEntity> TestEntities => this.Set<TestEntity>();
}

// Concrete repository used in tests.
internal class TestRepository : ElRosoRepositoryBase<TestEntity>
{
    public TestRepository(DbContext context, IConfiguration configuration, IHttpContextAccessor accessor, IMemoryCache cache)
        : base(context, configuration, accessor, NullLogger<TestRepository>.Instance, cache)
    {
    }
}

public class ElRosoRepositoryBaseTests : IDisposable
{
    private readonly TestDbContext dbContext;
    private readonly TestRepository sut;
    private readonly IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());

    public ElRosoRepositoryBaseTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        this.dbContext = new TestDbContext(options);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c.GetSection("DbCacheIsEnabled")).Returns(new Mock<IConfigurationSection>().Object);

        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        this.sut = new TestRepository(this.dbContext, configMock.Object, accessorMock.Object, this.cache);
    }

    private static TestEntity BuildEntity(string email = "a@b.com") => new()
    {
        Id = Guid.NewGuid(),
        CreationDate = DateTime.UtcNow,
        ModificationDate = DateTime.UtcNow,
        Name = "Test",
        Email = email,
    };

    [Fact]
    public async Task AddAsync_PersistsEntity()
    {
        var entity = BuildEntity();

        await this.sut.AddAsync(entity);

        Assert.True(await this.dbContext.TestEntities.AnyAsync(e => e.Id == entity.Id));
    }

    [Fact]
    public async Task AddAsync_SetsCreationDate()
    {
        var entity = BuildEntity();
        var before = DateTime.UtcNow;

        var result = await this.sut.AddAsync(entity);

        Assert.True(result.CreationDate >= before);
    }

    [Fact]
    public async Task GetAsync_ById_ReturnsCorrectEntity()
    {
        var entity = BuildEntity();
        await this.sut.AddAsync(entity);

        var result = await this.sut.GetAsync(entity.Id);

        Assert.NotNull(result);
        Assert.Equal(entity.Id, result.Id);
    }

    [Fact]
    public async Task GetAsync_ById_NonExistentId_ReturnsNull()
    {
        var result = await this.sut.GetAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task IsExistAsync_ExistingId_ReturnsTrue()
    {
        var entity = BuildEntity();
        await this.sut.AddAsync(entity);

        var result = await this.sut.IsExistAsync(entity.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task IsExistAsync_NonExistentId_ReturnsFalse()
    {
        var result = await this.sut.IsExistAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesEntity()
    {
        var entity = BuildEntity();
        await this.sut.AddAsync(entity);
        entity.Name = "Updated";

        await this.sut.UpdateAsync(entity);

        var updated = await this.sut.GetAsync(entity.Id);
        Assert.Equal("Updated", updated!.Name);
    }

    [Fact]
    public async Task UpdateAsync_PreservesCreationDate()
    {
        var entity = BuildEntity();
        var added = await this.sut.AddAsync(entity);
        var originalCreationDate = added.CreationDate;

        added.Name = "Changed";
        await this.sut.UpdateAsync(added);

        var result = await this.sut.GetAsync(added.Id);
        Assert.Equal(originalCreationDate, result!.CreationDate);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntity()
    {
        var entity = BuildEntity();
        await this.sut.AddAsync(entity);
        var toDelete = await this.sut.GetAsync(entity.Id);

        await this.sut.DeleteAsync(toDelete!);

        Assert.False(await this.sut.IsExistAsync(entity.Id));
    }

    [Fact]
    public async Task GetAllInPaginationAsync_ReturnsCorrectPage()
    {
        for (int i = 0; i < 10; i++)
        {
            await this.sut.AddAsync(BuildEntity($"user{i}@test.com"));
        }

        var page1 = (await this.sut.GetAllInPaginationAsync(1, 3)).ToList();
        var page2 = (await this.sut.GetAllInPaginationAsync(2, 3)).ToList();

        Assert.Equal(3, page1.Count);
        Assert.Equal(3, page2.Count);
        Assert.Empty(page1.Intersect(page2, EqualityComparer<TestEntity>.Default));
    }

    [Fact]
    public async Task UpdateAsync_NonExistentEntity_ThrowsNotFoundException()
    {
        var nonExistent = BuildEntity("ghost@test.com");
        nonExistent.Id = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => this.sut.UpdateAsync(nonExistent));
    }

    [Fact]
    public async Task GetAsync_All_ReturnsAllEntities()
    {
        await this.sut.AddAsync(BuildEntity("a@test.com"));
        await this.sut.AddAsync(BuildEntity("b@test.com"));

        var all = await this.sut.GetAsync();

        Assert.True(all.Count() >= 2);
    }

    [Fact]
    public async Task GetAsync_ConcurrentColdCacheRequests_AllReturnCorrectResults()
    {
        await this.sut.AddAsync(BuildEntity("c1@test.com"));
        await this.sut.AddAsync(BuildEntity("c2@test.com"));

        // Invalidate cache to guarantee a cold-cache scenario.
        // Invalidar caché para garantizar el escenario de caché frío.
        this.cache.Remove(typeof(TestEntity).Name);

        const int concurrency = 20;
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, concurrency)
            .Select(_ => Task.Run(async () =>
            {
                await barrier.Task;
                return await this.sut.GetAsync();
            }))
            .ToList();

        barrier.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.Count() >= 2));
        var expectedIds = results[0].Select(e => e.Id).OrderBy(id => id).ToList();
        Assert.All(results, r => Assert.Equal(expectedIds, r.Select(e => e.Id).OrderBy(id => id).ToList()));
    }

    // EN: A repository that caps at 3 rows instead of 10.000, so the truncation path can be tested
    //     without inserting ten thousand entities.
    // ES: Un repositorio que corta en 3 filas en vez de 10.000, para poder testear el camino de
    //     truncado sin insertar diez mil entidades.
    private sealed class CappedRepository : ElRosoRepositoryBase<TestEntity>
    {
        public CappedRepository(DbContext context, IConfiguration configuration, IHttpContextAccessor accessor, ILogger logger, IMemoryCache cache)
            : base(context, configuration, accessor, logger, cache)
        {
        }

        protected override int MaxRecordsLimit => 3;
    }

    private static IConfiguration BuildConfiguration(bool cacheEnabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DbCacheIsEnabled"] = cacheEnabled.ToString() })
            .Build();

    [Fact]
    public async Task GetAsync_WhenTheResultIsTruncated_WarnsInsteadOfSayingNothing()
    {
        // EN: The cap exists to keep a huge table from taking the process down, but it used to cut
        //     silently: a table of 10.001 rows returned 10.000 and nobody found out.
        // ES: El tope existe para que una tabla enorme no tumbe el proceso, pero cortaba en
        //     silencio: una tabla de 10.001 filas devolvía 10.000 y nadie se enteraba.
        var logger = new Mock<ILogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        var sut = new CappedRepository(this.dbContext, BuildConfiguration(cacheEnabled: true), accessorMock.Object, logger.Object, this.cache);

        for (var i = 0; i < 5; i++)
        {
            await sut.AddAsync(BuildEntity($"{i}@b.com"));
        }

        var result = await sut.GetAsync();

        Assert.Equal(3, result.Count());
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("truncated", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAsync_WhenTheResultIsTruncated_DoesNotCacheIt()
    {
        // EN: Caching a truncated result hands the same incomplete answer back for 20 minutes, as
        //     if it were the whole table.
        // ES: Cachear un resultado truncado devuelve la misma respuesta incompleta durante 20
        //     minutos, como si fuera la tabla entera.
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        var sut = new CappedRepository(this.dbContext, BuildConfiguration(cacheEnabled: true), accessorMock.Object, NullLogger.Instance, this.cache);

        for (var i = 0; i < 5; i++)
        {
            await sut.AddAsync(BuildEntity($"{i}@b.com"));
        }

        await sut.GetAsync();

        Assert.False(this.cache.TryGetValue(sut.KeyNameCache, out _));
    }

    [Fact]
    public async Task GetAsync_WhenTheResultFitsUnderTheCap_CachesIt()
    {
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        var sut = new CappedRepository(this.dbContext, BuildConfiguration(cacheEnabled: true), accessorMock.Object, NullLogger.Instance, this.cache);

        await sut.AddAsync(BuildEntity());

        await sut.GetAsync();

        Assert.True(this.cache.TryGetValue(sut.KeyNameCache, out _));
    }

    [Fact]
    public void Dispose_DoesNotDisposeTheInjectedDbContext()
    {
        // EN: The repository does not own the DbContext — the DI container does. Disposing it meant
        //     the first repository disposed in a request left every other DAO of that same request
        //     holding a dead context.
        // ES: El repositorio no es dueño del DbContext — lo es el contenedor de DI. Disponerlo hacía
        //     que el primer repositorio dispuesto de un request dejara a todos los demás DAOs de ese
        //     mismo request con un contexto muerto.
        this.sut.Dispose();

        var exception = Record.Exception(() => this.dbContext.TestEntities.Any());

        Assert.Null(exception);
    }

    public void Dispose()
    {
        this.dbContext.Dispose();
        this.cache.Dispose();
        GC.SuppressFinalize(this);
    }
}
