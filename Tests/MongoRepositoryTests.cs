namespace ElRoso.Commons.Tests;

using ElRoso.Domains.Entities.NoSQL;
using ElRoso.Domains.Session;
using ElRoso.Providers.NoSQL;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;

public class TestDocument : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
}

public sealed class TestDocumentRepository(
    IMongoDatabase database,
    IHttpContextAccessor accessor,
    ILogger<Repository<TestDocument>>? logger = null)
    : Repository<TestDocument>(database, logger ?? NullLogger<Repository<TestDocument>>.Instance, accessor)
{
    public override string NameCollection => "test-documents";
}

/// <summary>
/// EN: Same repository with a cap of 3, so the truncation tests do not need 10.001 documents.
/// ES: El mismo repositorio con un tope de 3, para que los tests de truncado no necesiten 10.001
///     documentos.
/// </summary>
public sealed class CappedTestDocumentRepository(
    IMongoDatabase database,
    ILogger<Repository<TestDocument>> logger)
    : Repository<TestDocument>(database, logger, httpContextAccessor: null)
{
    public override string NameCollection => "test-documents";

    protected override int MaxRecordsLimit => 3;
}

public class MongoRepositoryTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    private readonly Mock<IMongoCollection<TestDocument>> collection = new();
    private readonly Mock<IMongoDatabase> database = new();

    private TestDocumentRepository CreateSut(bool withCurrentUser)
    {
        this.database
            .Setup(d => d.GetCollection<TestDocument>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(this.collection.Object);

        var accessor = new Mock<IHttpContextAccessor>();

        if (withCurrentUser)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items["currentUser"] = new CurrentUser
            {
                Id = TestUserId,
                Surname = "Doe",
                GivenName = "John",
                EmailAddress = "john.doe@elroso.ar",
            };
            accessor.Setup(a => a.HttpContext).Returns(httpContext);
        }
        else
        {
            accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        }

        return new TestDocumentRepository(this.database.Object, accessor.Object);
    }

    [Fact]
    public async Task InsertAsync_StampsTheCreationDates()
    {
        var sut = this.CreateSut(withCurrentUser: false);
        var entity = new TestDocument { Nombre = "uno" };
        var before = DateTime.UtcNow;

        await sut.InsertAsync(entity);

        Assert.True(entity.Audit.CreationDate >= before);
        Assert.Equal(entity.Audit.CreationDate, entity.Audit.ModificationDate);
    }

    [Fact]
    public async Task InsertAsync_RecordsWhoCreatedTheDocument()
    {
        // EN: The Audit object has CreatedBy and ModifiedBy, so documents looked audited. The
        //     repository never filled them — unlike the SQL one, it never read CurrentUser — so
        //     every document ever written has them as null. The fields existing and always being
        //     empty is worse than not having them: it looks like a trail and it is not.
        // ES: El objeto Audit tiene CreatedBy y ModifiedBy, así que los documentos parecían
        //     auditados. El repositorio nunca los completaba — a diferencia del de SQL, nunca leía
        //     CurrentUser — así que todo documento escrito los tiene en null. Que los campos existan
        //     y estén siempre vacíos es peor que no tenerlos: parece una traza y no lo es.
        var sut = this.CreateSut(withCurrentUser: true);
        var entity = new TestDocument { Nombre = "uno" };

        await sut.InsertAsync(entity);

        Assert.Equal(TestUserId, entity.Audit.CreatedBy);
        Assert.Equal(TestUserId, entity.Audit.ModifiedBy);
    }

    [Fact]
    public async Task InsertAsync_WithoutAnAuthenticatedUser_LeavesTheAuthorEmpty()
    {
        var sut = this.CreateSut(withCurrentUser: false);
        var entity = new TestDocument { Nombre = "uno" };

        await sut.InsertAsync(entity);

        Assert.Null(entity.Audit.CreatedBy);
    }

    [Fact]
    public async Task InsertAsync_Bulk_RecordsWhoCreatedEveryDocument()
    {
        var sut = this.CreateSut(withCurrentUser: true);
        var entities = new List<TestDocument>
        {
            new() { Nombre = "uno" },
            new() { Nombre = "dos" },
        };

        await sut.InsertAsync(entities);

        Assert.All(entities, e => Assert.Equal(TestUserId, e.Audit.CreatedBy));
    }

    [Fact]
    public async Task UpdateAsync_RecordsWhoModifiedTheDocument_AndKeepsTheOriginalAuthor()
    {
        // EN: An update must not rewrite who created the document — that is the half of the trail
        //     that cannot be reconstructed afterwards.
        // ES: Un update no debe reescribir quién creó el documento — ésa es la mitad de la traza que
        //     no se puede reconstruir después.
        var originalAuthor = Guid.NewGuid();
        var sut = this.CreateSut(withCurrentUser: true);
        var entity = new TestDocument { Nombre = "uno" };
        entity.Audit.CreatedBy = originalAuthor;

        await sut.UpdateAsync(entity);

        Assert.Equal(TestUserId, entity.Audit.ModifiedBy);
        Assert.Equal(originalAuthor, entity.Audit.CreatedBy);
    }

    [Fact]
    public async Task UpdateAsync_StampsTheModificationDate()
    {
        var sut = this.CreateSut(withCurrentUser: false);
        var entity = new TestDocument { Nombre = "uno" };
        var before = DateTime.UtcNow;

        await sut.UpdateAsync(entity);

        Assert.True(entity.Audit.ModificationDate >= before);
    }

    [Fact]
    public async Task UpdateAsync_Bulk_RecordsWhoModifiedEveryDocument()
    {
        var sut = this.CreateSut(withCurrentUser: true);
        var entities = new List<TestDocument>
        {
            new() { Nombre = "uno" },
            new() { Nombre = "dos" },
        };

        await sut.UpdateAsync(entities);

        Assert.All(entities, e => Assert.Equal(TestUserId, e.Audit.ModifiedBy));
    }

    [Fact]
    public async Task InsertAsync_SendsTheDocumentToTheCollection()
    {
        var sut = this.CreateSut(withCurrentUser: false);
        var entity = new TestDocument { Nombre = "uno" };

        await sut.InsertAsync(entity);

        this.collection.Verify(
            c => c.InsertOneAsync(entity, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesInPlaceInsteadOfDeletingAndInserting()
    {
        var sut = this.CreateSut(withCurrentUser: false);
        var entity = new TestDocument { Nombre = "uno" };

        await sut.UpdateAsync(entity);

        this.collection.Verify(
            c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<TestDocument>>(),
                entity,
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Bulk_UsesASingleBulkWrite()
    {
        // EN: The bulk update walked the list calling ReplaceOneAsync one document at a time, so
        //     updating N documents cost N round trips to the server — the round trip, not the write,
        //     is what dominates. A batch of 500 documents over a 5 ms link spent 2,5 seconds waiting
        //     on the network. One BulkWrite sends all of them in a single trip.
        // ES: El update masivo recorría la lista llamando a ReplaceOneAsync de a un documento, así
        //     que actualizar N documentos costaba N round trips al servidor — lo que pesa es el round
        //     trip, no la escritura. Un batch de 500 documentos con un enlace de 5 ms se pasaba 2,5
        //     segundos esperando a la red. Un solo BulkWrite los manda todos en un viaje.
        var sut = this.CreateSut(withCurrentUser: false);
        var entities = new List<TestDocument>
        {
            new() { Nombre = "uno" },
            new() { Nombre = "dos" },
            new() { Nombre = "tres" },
        };

        await sut.UpdateAsync(entities);

        this.collection.Verify(
            c => c.BulkWriteAsync(
                It.Is<IEnumerable<WriteModel<TestDocument>>>(models => models.Count() == 3 && models.All(m => m is ReplaceOneModel<TestDocument>)),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        this.collection.Verify(
            c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<TestDocument>>(),
                It.IsAny<TestDocument>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Bulk_WithAnEmptyCollection_DoesNotTouchTheDatabase()
    {
        // EN: BulkWriteAsync throws on an empty request list, so an empty batch — which the old
        //     loop simply ignored — would now blow up unless it returns early.
        // ES: BulkWriteAsync tira excepción con la lista vacía, así que un batch vacío — que el loop
        //     viejo simplemente ignoraba — explotaría si no se corta antes.
        var sut = this.CreateSut(withCurrentUser: false);

        await sut.UpdateAsync(new List<TestDocument>());

        this.collection.Verify(
            c => c.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<TestDocument>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WithAnEmptyCollection_DoesNotTouchTheDatabase()
    {
        var sut = this.CreateSut(withCurrentUser: false);

        await sut.DeleteAsync(new List<TestDocument>());

        this.collection.Verify(
            c => c.DeleteManyAsync(It.IsAny<FilterDefinition<TestDocument>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_Bulk_UsesASingleDeleteMany()
    {
        var sut = this.CreateSut(withCurrentUser: false);
        var entities = new List<TestDocument>
        {
            new() { Nombre = "uno" },
            new() { Nombre = "dos" },
        };

        await sut.DeleteAsync(entities);

        this.collection.Verify(
            c => c.DeleteManyAsync(It.IsAny<FilterDefinition<TestDocument>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Consultas: filtro, conteo, existencia, página y tope ─────────────────────────────────────

    private static IAsyncCursor<TestDocument> CursorOf(params TestDocument[] documents)
    {
        var cursor = new Mock<IAsyncCursor<TestDocument>>();
        var served = false;

        cursor.Setup(c => c.Current).Returns(documents);
        cursor.Setup(c => c.MoveNext(It.IsAny<CancellationToken>())).Returns(() => !served && (served = true));
        cursor.Setup(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => !served && (served = true));

        return cursor.Object;
    }

    private void SetupFind(params TestDocument[] documents) =>
        this.collection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<TestDocument>>(),
                It.IsAny<FindOptions<TestDocument, TestDocument>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CursorOf(documents));

    private static string Render(FilterDefinition<TestDocument> filter) =>
        filter.Render(new RenderArgs<TestDocument>(
            BsonSerializer.SerializerRegistry.GetSerializer<TestDocument>(),
            BsonSerializer.SerializerRegistry)).ToString();

    [Fact]
    public async Task GetAsync_WithAFilter_AsksMongoForTheFilteredDocuments()
    {
        // EN: There was no way to query this repository by anything other than the id: consumers had
        //     to pull the whole collection with GetAsync() and filter in memory, which moves every
        //     document over the network to throw most of them away.
        // ES: No había forma de consultar este repositorio por otra cosa que el id: los consumidores
        //     tenían que traerse la colección entera con GetAsync() y filtrar en memoria, lo que mueve
        //     todos los documentos por la red para tirar casi todos.
        var sut = this.CreateSut(withCurrentUser: false);
        this.SetupFind(new TestDocument { Nombre = "uno" });

        FilterDefinition<TestDocument>? sent = null;
        this.collection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<TestDocument>>(),
                It.IsAny<FindOptions<TestDocument, TestDocument>>(),
                It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<TestDocument>, FindOptions<TestDocument, TestDocument>, CancellationToken>((f, _, _) => sent = f)
            .ReturnsAsync(() => CursorOf(new TestDocument { Nombre = "uno" }));

        var result = await sut.GetAsync(x => x.Nombre == "uno");

        Assert.Single(result);
        Assert.Contains("Nombre", Render(sent!));
    }

    [Fact]
    public async Task GetAsync_WithoutFilter_CutsAtTheSafetyCapAndWarns()
    {
        // EN: GetAsync() had no cap at all — the SQL side stops at 10.000 rows, this one brought the
        //     whole collection into memory. A collection that outgrows the cap now gets cut AND says
        //     so: cutting in silence is how a partial answer gets mistaken for the full one.
        // ES: GetAsync() no tenía ningún tope — el lado SQL corta en 10.000 filas, éste se traía la
        //     colección entera a memoria. Una colección que pasa el tope ahora se corta Y avisa:
        //     cortar en silencio es como una respuesta parcial se confunde con la completa.
        var logger = new Mock<ILogger<Repository<TestDocument>>>();
        this.database
            .Setup(d => d.GetCollection<TestDocument>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(this.collection.Object);

        // Cuatro documentos con un tope de tres: uno de más, que es como se detecta el truncado.
        this.SetupFind(
            new TestDocument { Nombre = "uno" },
            new TestDocument { Nombre = "dos" },
            new TestDocument { Nombre = "tres" },
            new TestDocument { Nombre = "cuatro" });

        var sut = new CappedTestDocumentRepository(this.database.Object, logger.Object);

        var result = await sut.GetAsync();

        Assert.Equal(3, result.Count());
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAsync_AsksForOneDocumentBeyondTheCap()
    {
        // EN: That extra document is the whole trick: without it there is no way to tell a collection
        //     of exactly the cap from one with more to give.
        // ES: Ese documento de más es todo el truco: sin él no hay manera de distinguir una colección
        //     de exactamente el tope de una que tenía más para dar.
        var logger = new Mock<ILogger<Repository<TestDocument>>>();
        this.database
            .Setup(d => d.GetCollection<TestDocument>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(this.collection.Object);
        this.SetupFind();

        var sut = new CappedTestDocumentRepository(this.database.Object, logger.Object);

        await sut.GetAsync();

        this.collection.Verify(
            c => c.FindAsync(
                It.IsAny<FilterDefinition<TestDocument>>(),
                It.Is<FindOptions<TestDocument, TestDocument>>(o => o.Limit == 4),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CountAsync_CountsOnTheServer()
    {
        var sut = this.CreateSut(withCurrentUser: false);
        this.collection
            .Setup(c => c.CountDocumentsAsync(
                It.IsAny<FilterDefinition<TestDocument>>(),
                It.IsAny<CountOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var total = await sut.CountAsync();

        Assert.Equal(42, total);
    }

    [Fact]
    public async Task AnyAsync_AsksForASingleDocument()
    {
        // EN: Existence is a Limit(1), not a count and not a full fetch.
        // ES: La existencia es un Limit(1), no un conteo ni traer todo.
        var sut = this.CreateSut(withCurrentUser: false);
        this.SetupFind(new TestDocument { Nombre = "uno" });

        var exists = await sut.AnyAsync(x => x.Nombre == "uno");

        Assert.True(exists);
        this.collection.Verify(
            c => c.FindAsync(
                It.IsAny<FilterDefinition<TestDocument>>(),
                It.Is<FindOptions<TestDocument, TestDocument>>(o => o.Limit == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ReturnsThePageAndTheTotal()
    {
        var sut = this.CreateSut(withCurrentUser: false);
        this.SetupFind(new TestDocument { Nombre = "tres" }, new TestDocument { Nombre = "cuatro" });
        this.collection
            .Setup(c => c.CountDocumentsAsync(
                It.IsAny<FilterDefinition<TestDocument>>(),
                It.IsAny<CountOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var (items, total) = await sut.GetPageAsync(page: 2, pageSize: 2);

        Assert.Equal(2, items.Count);
        Assert.Equal(10, total);
        this.collection.Verify(
            c => c.FindAsync(
                It.IsAny<FilterDefinition<TestDocument>>(),
                It.Is<FindOptions<TestDocument, TestDocument>>(o => o.Skip == 2 && o.Limit == 2 && o.Sort != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(-1, 10)]
    public async Task GetPageAsync_WithAnInvalidPage_Fails(int page, int pageSize)
    {
        // EN: page 0 would compute a negative Skip and Mongo would reject it far from the caller.
        // ES: la página 0 daría un Skip negativo y Mongo lo rechazaría lejos de quien llamó.
        var sut = this.CreateSut(withCurrentUser: false);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.GetPageAsync(page, pageSize));
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithNoMatch_ReturnsNull()
    {
        var sut = this.CreateSut(withCurrentUser: false);
        this.SetupFind();

        var found = await sut.FirstOrDefaultAsync(x => x.Nombre == "no-existe");

        Assert.Null(found);
    }

    [Fact]
    public async Task DeleteCollectionAsync_DropsTheCollection()
    {
        var sut = this.CreateSut(withCurrentUser: false);

        await sut.DeleteCollectionAsync();

        this.database.Verify(
            d => d.DropCollectionAsync("test-documents", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
