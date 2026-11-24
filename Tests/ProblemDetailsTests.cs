namespace ElRoso.Commons.Tests
{
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using ElRoso.AspNetCore.Outcomes;
    using ElRoso.DependencyInjection;
    using ElRoso.Exceptions;
    using ElRoso.Outcomes;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class ProblemDetailsTests
    {
        private const string ProblemJson = "application/problem+json";

        [Fact]
        public async Task NotFoundResult_ReturnsProblemDetailsWithTheRequestPath()
        {
            await using var app = await StartAsync(endpoints: routes =>
                routes.MapGet("/recurso", () => Result.Failure(Error.NotFound("recurso-ausente", "El recurso no está disponible.")).ToHttpResult()));

            var response = await app.GetTestClient().GetAsync("/recurso");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = body.RootElement;
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("type").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
            Assert.Equal(404, root.GetProperty("status").GetInt32());
            Assert.Equal("El recurso no está disponible.", root.GetProperty("detail").GetString());
            Assert.Equal("/recurso", root.GetProperty("instance").GetString());
            Assert.Equal("recurso-ausente", root.GetProperty("code").GetString());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        }

        [Fact]
        public async Task ValidationResult_ReturnsProblemsGroupedByProperty()
        {
            var error = Error.Validation("datos-invalidos", "La solicitud no es válida.", new Dictionary<string, string[]>
            {
                ["name"] = ["El nombre es obligatorio."],
                ["age"] = ["La edad debe ser positiva."],
            });

            await using var app = await StartAsync(endpoints: routes =>
                routes.MapGet("/validar", () => Result.Failure(error).ToHttpResult()));

            var response = await app.GetTestClient().GetAsync("/validar");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = body.RootElement;
            Assert.Equal(400, root.GetProperty("status").GetInt32());
            Assert.Equal("datos-invalidos", root.GetProperty("code").GetString());
            var errors = root.GetProperty("errors");
            Assert.Equal("El nombre es obligatorio.", errors.GetProperty("name")[0].GetString());
            Assert.Equal("La edad debe ser positiva.", errors.GetProperty("age")[0].GetString());
        }

        [Theory]
        [InlineData(ErrorType.Validation, 400)]
        [InlineData(ErrorType.Unauthorized, 401)]
        [InlineData(ErrorType.Forbidden, 403)]
        [InlineData(ErrorType.NotFound, 404)]
        [InlineData(ErrorType.Conflict, 409)]
        [InlineData(ErrorType.Failure, 500)]
        public async Task EveryErrorType_MapsToItsStatusCode(ErrorType type, int expectedStatus)
        {
            var error = CreateError(type);

            await using var app = await StartAsync(endpoints: routes =>
                routes.MapGet("/falla", () => Result.Failure(error).ToHttpResult()));

            var response = await app.GetTestClient().GetAsync("/falla");

            Assert.Equal(expectedStatus, (int)response.StatusCode);
            Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(expectedStatus, body.RootElement.GetProperty("status").GetInt32());
        }

        [Fact]
        public async Task SuccessfulTypedResult_ReturnsTheValueAsJson()
        {
            await using var app = await StartAsync(endpoints: routes =>
                routes.MapGet("/valor", () => ((Result<string>)"listo").ToHttpResult()));

            var response = await app.GetTestClient().GetAsync("/valor");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("listo", body.RootElement.GetString());
        }

        [Fact]
        public async Task SuccessfulResult_ReturnsNoContent()
        {
            await using var app = await StartAsync(endpoints: routes =>
                routes.MapGet("/vacio", () => Result.Success().ToHttpResult()));

            var response = await app.GetTestClient().GetAsync("/vacio");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task ControllerResults_KeepTheSameHttpShapeAsMinimalApis()
        {
            await using var app = await StartAsync(
                register: services => services.AddControllers().AddApplicationPart(typeof(ResultProbeController).Assembly),
                endpoints: routes => routes.MapControllers());
            var client = app.GetTestClient();

            var missing = await client.GetAsync("/results/recurso");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal(ProblemJson, missing.Content.Headers.ContentType?.MediaType);
            using (var body = JsonDocument.Parse(await missing.Content.ReadAsStringAsync()))
            {
                Assert.Equal(404, body.RootElement.GetProperty("status").GetInt32());
                Assert.Equal("recurso-ausente", body.RootElement.GetProperty("code").GetString());
            }

            var value = await client.GetAsync("/results/valor");
            Assert.Equal(HttpStatusCode.OK, value.StatusCode);
            using (var body = JsonDocument.Parse(await value.Content.ReadAsStringAsync()))
            {
                Assert.Equal("listo", body.RootElement.GetString());
            }

            var empty = await client.GetAsync("/results/vacio");
            Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);
            Assert.Equal(string.Empty, await empty.Content.ReadAsStringAsync());

            var custom = await client.GetAsync("/results/personalizado");
            Assert.Equal(HttpStatusCode.OK, custom.StatusCode);
            using (var body = JsonDocument.Parse(await custom.Content.ReadAsStringAsync()))
            {
                Assert.Equal("listo", body.RootElement.GetProperty("value").GetString());
                Assert.Equal("personalizado", body.RootElement.GetProperty("marker").GetString());
            }
        }

        [Fact]
        public async Task DomainExceptions_AndNoContent_GetTheirMappedResponses()
        {
            await using var app = await StartAsync(environment: Environments.Production, endpoints: MapExceptionEndpoints);
            var client = app.GetTestClient();

            var notFound = await client.GetAsync("/errores/no-encontrado");
            Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
            using (var body = JsonDocument.Parse(await notFound.Content.ReadAsStringAsync()))
            {
                Assert.Equal(404, body.RootElement.GetProperty("status").GetInt32());
                Assert.Equal("El recurso no está disponible.", body.RootElement.GetProperty("detail").GetString());
            }

            var file = await client.GetAsync("/errores/archivo");
            Assert.Equal(HttpStatusCode.NotFound, file.StatusCode);

            var badRequest = await client.GetAsync("/errores/datos");
            Assert.Equal(HttpStatusCode.BadRequest, badRequest.StatusCode);
            using (var body = JsonDocument.Parse(await badRequest.Content.ReadAsStringAsync()))
            {
                Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
                var errors = body.RootElement.GetProperty("errors");
                Assert.Equal("El nombre es obligatorio.", errors.GetProperty("name")[0].GetString());
            }

            var empty = await client.GetAsync("/errores/sin-contenido");
            Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);
            Assert.Equal(string.Empty, await empty.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task UnknownException_HidesItsMessageInProduction()
        {
            await using var app = await StartAsync(environment: Environments.Production, endpoints: MapUnknownExceptionEndpoint);

            var response = await app.GetTestClient().GetAsync("/errores/desconocido");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
            Assert.DoesNotContain("fallo-interno-secreto", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task UnknownException_ShowsItsMessageInDevelopment()
        {
            await using var app = await StartAsync(environment: Environments.Development, endpoints: MapUnknownExceptionEndpoint);

            var response = await app.GetTestClient().GetAsync("/errores/desconocido");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Contains("fallo-interno-secreto", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task DomainException_LogsOneWarningAndNoError()
        {
            var spy = new CapturingLoggerProvider();

            await using var app = await StartAsync(
                environment: Environments.Production,
                configureLogging: logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(spy);
                },
                endpoints: routes => routes.MapGet("/dominio", (Func<string>)(() => throw new NotFoundException("El recurso no está disponible."))));

            var response = await app.GetTestClient().GetAsync("/dominio");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain(LogLevel.Error, spy.Entries.Select(entry => entry.Level));
            Assert.DoesNotContain(LogLevel.Critical, spy.Entries.Select(entry => entry.Level));
            Assert.Single(spy.Entries, entry => entry.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task UnknownException_LogsAnErrorWithTheException()
        {
            var spy = new CapturingLoggerProvider();
            var boom = new InvalidOperationException("fallo-interno-secreto");

            await using var app = await StartAsync(
                environment: Environments.Production,
                configureLogging: logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(spy);
                },
                endpoints: routes => routes.MapGet("/desconocido", (Func<string>)(() => throw boom)));

            var response = await app.GetTestClient().GetAsync("/desconocido");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Contains(LogLevel.Error, spy.Entries.Select(entry => entry.Level));
            Assert.Contains(boom, spy.Entries.Select(entry => entry.Exception));
        }

        [Fact]
        public async Task UnreadableBody_ReturnsValidationProblemDetails()
        {
            await using var app = await StartAsync(
                register: services =>
                {
                    services.AddControllersCustom(new ConfigurationBuilder().Build());
                    services.AddControllers().AddApplicationPart(typeof(ResultProbeController).Assembly);
                },
                endpoints: routes => routes.MapControllers());

            var response = await app.GetTestClient().PostAsync(
                "/results/cuerpo",
                new StringContent("{\"name\":", Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
            Assert.True(body.RootElement.GetProperty("errors").EnumerateObject().Any());
        }

        [Fact]
        public async Task UnknownRoute_ReturnsProblemDetailsFromStatusCodePages()
        {
            await using var app = await StartAsync();

            var response = await app.GetTestClient().GetAsync("/ruta-inexistente");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(404, body.RootElement.GetProperty("status").GetInt32());
        }

        [Fact]
        public void UseWithoutAdd_ExplainsTheMissingRegistration()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production,
                ApplicationName = typeof(ProblemDetailsTests).Assembly.GetName().Name,
            });
            builder.WebHost.UseTestServer();
            using var app = builder.Build();

            var exception = Assert.Throws<InvalidOperationException>(() => app.UseElRosoProblemDetails());

            Assert.Contains("AddElRosoProblemDetails", exception.Message, StringComparison.Ordinal);
        }

        private static Error CreateError(ErrorType type) => type switch
        {
            ErrorType.Validation => Error.Validation("datos-invalidos", "La solicitud no es válida.", new Dictionary<string, string[]>
            {
                ["name"] = ["El nombre es obligatorio."],
            }),
            ErrorType.Unauthorized => Error.Unauthorized("sin-autenticar", "Se requiere autenticación."),
            ErrorType.Forbidden => Error.Forbidden("sin-permiso", "El acceso no está permitido."),
            ErrorType.NotFound => Error.NotFound("recurso-ausente", "El recurso no está disponible."),
            ErrorType.Conflict => Error.Conflict("duplicado", "El recurso ya existe."),
            _ => Error.Failure("fallo", "La operación no se pudo completar."),
        };

        private static void MapExceptionEndpoints(IEndpointRouteBuilder routes)
        {
            routes.MapGet("/errores/no-encontrado", (Func<string>)(() => throw new NotFoundException("El recurso no está disponible.")));
            routes.MapGet("/errores/archivo", (Func<string>)(() => throw new ElRoso.Exceptions.FileNotFoundException("El objeto no está disponible.")));
            routes.MapGet("/errores/datos", (Func<string>)(() => throw new BadRequestException("La solicitud no es válida.")
            {
                Details = new Dictionary<string, string[]> { ["name"] = ["El nombre es obligatorio."] },
            }));
            routes.MapGet("/errores/sin-contenido", (Func<string>)(() => throw new NoContentException()));
        }

        private static void MapUnknownExceptionEndpoint(IEndpointRouteBuilder routes) =>
            routes.MapGet("/errores/desconocido", (Func<string>)(() => throw new InvalidOperationException("fallo-interno-secreto")));

        private static async Task<WebApplication> StartAsync(
            string? environment = null,
            Action<IServiceCollection>? register = null,
            Action<ILoggingBuilder>? configureLogging = null,
            Action<IEndpointRouteBuilder>? endpoints = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = environment ?? Environments.Production,
                ApplicationName = typeof(ProblemDetailsTests).Assembly.GetName().Name,
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddElRosoProblemDetails();
            register?.Invoke(builder.Services);
            configureLogging?.Invoke(builder.Logging);

            var app = builder.Build();
            app.UseElRosoProblemDetails();
            endpoints?.Invoke(app);
            await app.StartAsync();
            return app;
        }

        private sealed class CapturingLoggerProvider : ILoggerProvider
        {
            public List<(LogLevel Level, Exception? Exception, string Category)> Entries { get; } = new();

            public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

            public void Dispose()
            {
            }

            private sealed class CapturingLogger(CapturingLoggerProvider provider, string category) : ILogger
            {
                public IDisposable? BeginScope<TState>(TState state)
                    where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(
                    LogLevel logLevel,
                    EventId eventId,
                    TState state,
                    Exception? exception,
                    Func<TState, Exception?, string> formatter)
                    => provider.Entries.Add((logLevel, exception, category));
            }
        }
    }

    [ApiController]
    [Route("results")]
    public sealed class ResultProbeController : ControllerBase
    {
        [HttpGet("recurso")]
        public IActionResult Missing() =>
            Result.Failure(Error.NotFound("recurso-ausente", "El recurso no está disponible.")).ToActionResult(this);

        [HttpGet("valor")]
        public IActionResult Value() => ((Result<string>)"listo").ToActionResult(this);

        [HttpGet("vacio")]
        public IActionResult EmptyResult() => Result.Success().ToActionResult(this);

        [HttpGet("personalizado")]
        public IActionResult Custom() => ((Result<string>)"listo").ToActionResult(this, value => this.Ok(new { value, marker = "personalizado" }));

        [HttpPost("cuerpo")]
        public IActionResult Body([FromBody] ProblemRequestBody request) => this.Ok(request.Name);
    }

    public sealed class ProblemRequestBody
    {
        public string Name { get; set; } = string.Empty;
    }
}

namespace ElRoso.AspNetCore.Outcomes
{
    // Declara el namespace publico de las extensiones de resultados.
}
