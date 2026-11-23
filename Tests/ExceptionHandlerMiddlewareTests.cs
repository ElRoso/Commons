namespace ElRoso.Commons.Tests;

using ElRoso.DependencyInjection;
using ElRoso.Exceptions;
using ElRoso.Presentation.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Text.Json;

public class ExceptionHandlerMiddlewareTests
{
    private readonly Mock<IHostEnvironment> envMock = new();
    private readonly IMemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());

    private ExceptionHandlerMiddleware CreateSut() =>
        new(envMock.Object, NullLogger<ExceptionHandlerMiddleware>.Instance);

    private static DefaultHttpContext CreateHttpContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new System.IO.MemoryStream();
        return ctx;
    }

    [Fact]
    public async Task InvokeAsync_NoException_PassesThrough()
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Development");
        var sut = CreateSut();
        var ctx = CreateHttpContext();
        var called = false;

        await sut.InvokeAsync(ctx, _ => { called = true; return Task.CompletedTask; });

        Assert.True(called);
    }

    [Theory]
    [InlineData(typeof(BadRequestException), HttpStatusCode.BadRequest)]
    [InlineData(typeof(UnauthorizedException), HttpStatusCode.Unauthorized)]
    [InlineData(typeof(ForbiddenException), HttpStatusCode.Forbidden)]
    [InlineData(typeof(NotFoundException), HttpStatusCode.NotFound)]
    [InlineData(typeof(NoContentException), HttpStatusCode.NoContent)]
    [InlineData(typeof(NotImplementedException), HttpStatusCode.NotImplemented)]
    [InlineData(typeof(ElRoso.Exceptions.FileNotFoundException), HttpStatusCode.NotFound)]
    public async Task InvokeAsync_KnownException_ReturnsMappedStatusCode(Type exceptionType, HttpStatusCode expectedStatus)
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Development");
        var sut = CreateSut();
        var ctx = CreateHttpContext();
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        await sut.InvokeAsync(ctx, _ => throw exception);

        Assert.Equal((int)expectedStatus, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnknownException_Returns500()
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Development");
        var sut = CreateSut();
        var ctx = CreateHttpContext();

        await sut.InvokeAsync(ctx, _ => throw new InvalidOperationException("boom"));

        Assert.Equal(500, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_InProduction_HidesExceptionMessage()
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        var sut = CreateSut();
        var ctx = CreateHttpContext();

        await sut.InvokeAsync(ctx, _ => throw new InvalidOperationException("internal secret details"));

        ctx.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        var body = await new System.IO.StreamReader(ctx.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonElement>(body);
        var title = response.GetProperty("title").GetString();

        Assert.DoesNotContain("internal secret details", title);
    }

    [Fact]
    public async Task InvokeAsync_InDevelopment_ShowsExceptionMessage()
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Development");
        var sut = CreateSut();
        var ctx = CreateHttpContext();

        await sut.InvokeAsync(ctx, _ => throw new InvalidOperationException("visible dev detail"));

        ctx.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        var body = await new System.IO.StreamReader(ctx.Response.Body).ReadToEndAsync();

        Assert.Contains("visible dev detail", body);
    }

    // EN: 🔴 This used to assert the opposite: an unhandled exception wiped the WHOLE IMemoryCache.
    //     The process has exactly one, and consuming apps keep security counters in it — a login
    //     throttle per email, permission stamps. So anyone able to trigger a 500 (no authentication
    //     needed on several endpoints) could reset the brute-force throttle at will, and every
    //     tenant's cached listings reloaded at once on top of that.
    //     The flush was a guess that the failure came from stale cached state. There is no case
    //     behind it: it dates from the first commit with no rationale, and the only later change
    //     narrowed it. Whoever writes a cache entry knows which key went bad and can invalidate
    //     that one — a global flush is not the middleware's call to make.
    // ES: 🔴 Esto antes afirmaba lo contrario: una excepción no manejada vaciaba el IMemoryCache
    //     ENTERO. El proceso tiene uno solo, y las apps consumidoras guardan ahí contadores de
    //     seguridad — un throttle de login por email, los permission stamps. Así que cualquiera capaz
    //     de disparar un 500 (varios endpoints ni piden autenticación) podía resetear el freno de
    //     fuerza bruta a voluntad, y encima los listados cacheados de todos los tenants recargaban
    //     a la vez.
    //     El borrado era una conjetura de que la falla venía de un dato cacheado en mal estado. No
    //     hay ningún caso detrás: viene del primer commit sin justificación, y el único cambio
    //     posterior lo acotó. Quien escribe una entrada de caché sabe qué clave quedó mal y puede
    //     invalidar ésa — un borrado global no es una decisión del middleware.
    [Fact]
    public async Task InvokeAsync_UnknownException_DoesNotWipeTheSharedCache()
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Development");
        memoryCache.Set("some-key", "some-value");
        var sut = CreateSut();
        var ctx = CreateHttpContext();

        await sut.InvokeAsync(ctx, _ => throw new InvalidOperationException("unexpected"));

        Assert.True(memoryCache.TryGetValue("some-key", out _));
    }

    // EN: The bypass this closes, written the way it was exploited: fail logins until the per-email
    //     throttle trips, then throw ANY non-domain exception to clear the counter and start over.
    //     The per-IP rate limiter does not live in IMemoryCache, so it never noticed; the per-email
    //     throttle — the one that exists precisely because the attacker rotates addresses — did.
    // ES: El bypass que esto cierra, escrito como se explotaba: fallar logins hasta que salte el
    //     throttle por email y después tirar CUALQUIER excepción no-dominio para limpiar el contador
    //     y arrancar de cero. El rate limiter por IP no vive en el IMemoryCache, así que nunca se
    //     enteró; el throttle por email — el que existe justamente porque el atacante rota
    //     direcciones — sí.
    [Fact]
    public async Task InvokeAsync_UnknownException_KeepsTheBruteForceThrottleCounter()
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        const string throttleKey = "login_throttle:victima@empresa.com";
        memoryCache.Set(throttleKey, 10);
        var sut = CreateSut();
        var ctx = CreateHttpContext();

        await sut.InvokeAsync(ctx, _ => throw new NullReferenceException("boom"));

        Assert.True(memoryCache.TryGetValue(throttleKey, out int attempts));
        Assert.Equal(10, attempts);
    }

    [Theory]
    [InlineData(typeof(BadRequestException))]
    [InlineData(typeof(UnauthorizedException))]
    [InlineData(typeof(ForbiddenException))]
    [InlineData(typeof(NotFoundException))]
    [InlineData(typeof(NoContentException))]
    [InlineData(typeof(NotImplementedException))]
    public async Task InvokeAsync_DomainException_DoesNotClearCache(Type exceptionType)
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        memoryCache.Set("tenant-key", "tenant-value");
        var sut = CreateSut();
        var ctx = CreateHttpContext();
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        await sut.InvokeAsync(ctx, _ => throw exception);

        Assert.True(memoryCache.TryGetValue("tenant-key", out _));
    }

    [Fact]
    public async Task InvokeAsync_InProduction_DomainException_ShowsMessage()
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        var sut = CreateSut();
        var ctx = CreateHttpContext();
        const string businessMessage = "El monto no puede ser negativo.";

        await sut.InvokeAsync(ctx, _ => throw new BadRequestException(businessMessage));

        ctx.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        var body = await new System.IO.StreamReader(ctx.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonElement>(body);
        var title = response.GetProperty("title").GetString();

        Assert.Equal(businessMessage, title);
    }

    // EN: The three-argument constructor stays alive on purpose — four backends consume this
    //     package and some build the middleware by hand in their own tests. It ignores the cache.
    // ES: El constructor de tres argumentos sigue vivo a propósito — cuatro backends consumen este
    //     paquete y alguno construye el middleware a mano en sus propios tests. Ignora la caché.
    [Fact]
    public async Task TheObsoleteConstructorStillWorksAndIgnoresTheCache()
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        memoryCache.Set("tenant-key", "tenant-value");
#pragma warning disable CS0618 // EN: exercising the compatibility overload on purpose. / ES: se ejercita a propósito la sobrecarga de compatibilidad.
        var sut = new ExceptionHandlerMiddleware(envMock.Object, NullLogger<ExceptionHandlerMiddleware>.Instance, memoryCache);
#pragma warning restore CS0618
        var ctx = CreateHttpContext();

        await sut.InvokeAsync(ctx, _ => throw new InvalidOperationException("boom"));

        Assert.Equal(500, ctx.Response.StatusCode);
        Assert.True(memoryCache.TryGetValue("tenant-key", out _));
    }

    // EN: 🔴 The middleware is resolved from the container, and it now has TWO constructors. If the
    //     container could not choose between them it would throw on the first request of every one
    //     of the four backends that consume this package. It picks the widest one it can satisfy —
    //     this pins that down, because the failure would only show up at runtime.
    // ES: 🔴 El middleware lo resuelve el contenedor, y ahora tiene DOS constructores. Si no pudiera
    //     elegir entre ellos, reventaría en el primer request de cada uno de los cuatro backends que
    //     consumen el paquete. Elige el más ancho que puede satisfacer — esto lo deja fijado, porque
    //     la falla solo aparecería en runtime.
    [Fact]
    public void TheContainerCanStillActivateTheMiddleware()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton(envMock.Object);
        services.AddExceptionHandlerMiddleware();

        var resolved = services.BuildServiceProvider().GetRequiredService<ExceptionHandlerMiddleware>();

        Assert.NotNull(resolved);
    }

    [Fact]
    public async Task InvokeAsync_SetsContentTypeToProblemJson()
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Development");
        var sut = CreateSut();
        var ctx = CreateHttpContext();

        await sut.InvokeAsync(ctx, _ => throw new NotFoundException());

        Assert.Equal("application/problem+json", ctx.Response.ContentType);
    }

    // Una excepción de dominio se loguea en Warning, no en Error.
    [Theory]
    [InlineData(typeof(BadRequestException))]
    [InlineData(typeof(UnauthorizedException))]
    [InlineData(typeof(ForbiddenException))]
    [InlineData(typeof(NotFoundException))]
    [InlineData(typeof(NoContentException))]
    public async Task InvokeAsync_DomainException_DoesNotPolluteTheErrorLog(Type exceptionType)
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        var spy = new CapturingLogger();
        var sut = new ExceptionHandlerMiddleware(envMock.Object, spy);
        var ctx = CreateHttpContext();
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        await sut.InvokeAsync(ctx, _ => throw exception);

        Assert.DoesNotContain(LogLevel.Error, spy.Levels);
        Assert.DoesNotContain(LogLevel.Critical, spy.Levels);
        Assert.Contains(LogLevel.Warning, spy.Levels);
    }

    // EN: 🔴 The half that keeps the change honest. An unknown exception IS an application failure
    //     and must stay at Error, with its stack trace — quieting these would trade noise for
    //     blindness.
    // ES: 🔴 La mitad que mantiene honesto el cambio. Una excepción desconocida SÍ es una falla de la
    //     aplicación y tiene que seguir en Error, con su stack trace — callar éstas cambiaría ruido
    //     por ceguera.
    [Fact]
    public async Task InvokeAsync_UnknownException_StillLogsAtError()
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        var spy = new CapturingLogger();
        var sut = new ExceptionHandlerMiddleware(envMock.Object, spy);
        var ctx = CreateHttpContext();

        await sut.InvokeAsync(ctx, _ => throw new InvalidOperationException("boom"));

        Assert.Contains(LogLevel.Error, spy.Levels);
    }

    [Fact]
    public async Task InvokeAsync_UnknownException_KeepsTheExceptionAttachedToTheLog()
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        var spy = new CapturingLogger();
        var sut = new ExceptionHandlerMiddleware(envMock.Object, spy);
        var ctx = CreateHttpContext();
        var boom = new InvalidOperationException("boom");

        await sut.InvokeAsync(ctx, _ => throw boom);

        Assert.Contains(boom, spy.Exceptions);
    }

    // EN: A spy rather than a Moq setup: ILogger.Log is generic, so asserting on it through Moq is
    //     mostly ceremony about TState. This records what matters — level and exception.
    // ES: Un espía en vez de un setup de Moq: ILogger.Log es genérico, así que afirmar sobre él con
    //     Moq es casi todo ceremonia sobre TState. Esto registra lo que importa — nivel y excepción.
    private sealed class CapturingLogger : ILogger<ExceptionHandlerMiddleware>
    {
        public List<LogLevel> Levels { get; } = new();

        public List<Exception?> Exceptions { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            this.Levels.Add(logLevel);
            this.Exceptions.Add(exception);
        }
    }

    // EN: A domain exception defined by a consuming app on top of ours must map to its parent's
    //     status. The middleware used to compare types exactly, so any subclass silently fell
    //     through to 500 — which is how a missing file ended up as an internal server error.
    // ES: Una excepción de dominio que una app consumidora define sobre las nuestras tiene que
    //     mapear al status de su padre. El middleware comparaba tipos exactos, así que toda
    //     subclase caía en silencio a 500 — así es como un archivo inexistente terminaba siendo un
    //     error interno del servidor.
    private sealed class ProductoNotFoundException : NotFoundException
    {
        public ProductoNotFoundException()
            : base("El producto no existe")
        {
        }
    }

    [Fact]
    public async Task InvokeAsync_SubclassOfADomainException_MapsToItsParentStatus()
    {
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        var sut = CreateSut();
        var ctx = CreateHttpContext();

        await sut.InvokeAsync(ctx, _ => throw new ProductoNotFoundException());

        Assert.Equal((int)HttpStatusCode.NotFound, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_SubclassOfADomainException_KeepsItsMessageInProduction()
    {
        // EN: It is a domain exception, so its message is meant for whoever called — masking it
        //     would leave a 404 with no explanation.
        // ES: Es una excepción de dominio, así que su mensaje es para quien llamó — enmascararlo
        //     dejaría un 404 sin explicación.
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        var sut = CreateSut();
        var ctx = CreateHttpContext();

        await sut.InvokeAsync(ctx, _ => throw new ProductoNotFoundException());

        ctx.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        var payload = await new System.IO.StreamReader(ctx.Response.Body).ReadToEndAsync();

        Assert.Contains("El producto no existe", payload);
    }

    [Fact]
    public async Task InvokeAsync_SystemIOFileNotFound_IsStillAnInternalError()
    {
        // EN: Only OUR file-not-found is a domain exception. A System.IO failure reading something
        //     off the local disk is a genuine internal error and must not be dressed up as a 404.
        // ES: Solo NUESTRO archivo-no-encontrado es excepción de dominio. Una falla de System.IO
        //     leyendo algo del disco local es un error interno de verdad y no hay que disfrazarlo
        //     de 404.
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        var sut = CreateSut();
        var ctx = CreateHttpContext();

        await sut.InvokeAsync(ctx, _ => throw new System.IO.FileNotFoundException("/etc/secreto"));

        Assert.Equal((int)HttpStatusCode.InternalServerError, ctx.Response.StatusCode);
    }
}
