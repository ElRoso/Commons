namespace ElRoso.Commons.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using ElRoso.DependencyInjection;
using ElRoso.Exceptions;
using ElRoso.Presentation.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public class UnreadableBodyFilterTests
{
    private static ActionExecutingContext BuildContext(BindingSource source, object? argument, bool invalidModelState)
    {
        var descriptor = new ActionDescriptor
        {
            Parameters =
            [
                new ParameterDescriptor
                {
                    Name = "request",
                    ParameterType = typeof(Payload),
                    BindingInfo = new BindingInfo { BindingSource = source },
                },
            ],
        };

        var modelState = new ModelStateDictionary();
        if (invalidModelState)
        {
            modelState.AddModelError("$.kind", "The JSON value could not be converted.");
        }

        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor, modelState);
        var arguments = new Dictionary<string, object?>();
        if (argument is not null)
        {
            arguments["request"] = argument;
        }

        return new ActionExecutingContext(actionContext, [], arguments, controller: new object());
    }

    [Fact]
    public void OnActionExecuting_NullBodyWithInvalidModelState_ThrowsBadRequestWithDetails()
    {
        var context = BuildContext(BindingSource.Body, argument: null, invalidModelState: true);

        var exception = Assert.Throws<BadRequestException>(() => new UnreadableBodyFilter().OnActionExecuting(context));

        Assert.Equal("El cuerpo del pedido no es válido.", exception.Message);
        Assert.Equal(["The JSON value could not be converted."], exception.Details["$.kind"]);
    }

    [Fact]
    public void OnActionExecuting_BodyPresent_DoesNotThrow()
    {
        var context = BuildContext(BindingSource.Body, new Payload(), invalidModelState: false);

        new UnreadableBodyFilter().OnActionExecuting(context);
    }

    [Fact]
    public void OnActionExecuting_WithoutBodyParameter_DoesNotThrow()
    {
        var context = BuildContext(BindingSource.Query, argument: null, invalidModelState: true);

        new UnreadableBodyFilter().OnActionExecuting(context);
    }

    [Fact]
    public void OnActionExecuting_InvalidModelStateWithBodyPresent_DoesNotThrow()
    {
        var context = BuildContext(BindingSource.Body, new Payload(), invalidModelState: true);

        new UnreadableBodyFilter().OnActionExecuting(context);
    }

    [Fact]
    public void AddControllersCustom_RegistersTheFilterGlobally()
    {
        var services = new ServiceCollection();
        services.AddControllersCustom(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var filters = provider.GetRequiredService<IOptions<MvcOptions>>().Value.Filters;

        Assert.Contains(filters, f => f is TypeFilterAttribute t && t.ImplementationType == typeof(UnreadableBodyFilter));
    }

    [Theory]
    [InlineData("{\"name\":")]
    [InlineData("{\"name\":\"a\",\"kind\":\"Second\"}")]
    public async Task Post_WithUnreadableBody_Returns400ProblemJson(string body)
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().PostAsync("/probe", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("El cuerpo del pedido no es válido.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_WithValidBody_Returns200()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().PostAsJsonAsync("/probe", new { name = "a", kind = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("a", await response.Content.ReadFromJsonAsync<string>());
    }

    private static async Task<IHost> StartHostAsync() =>
        await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.UseEnvironment("Development");
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddControllersCustom(new ConfigurationBuilder().Build())
                        .AddControllers()
                        .AddApplicationPart(typeof(ProbeController).Assembly);
                    services.AddExceptionHandlerMiddleware();
                });
                web.Configure(app =>
                {
                    app.UseExceptionHandlerMiddleware();
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync();
}

public enum PayloadKind
{
    First,
    Second,
}

public sealed class Payload
{
    public string Name { get; set; } = string.Empty;

    public PayloadKind Kind { get; set; }
}

[ApiController]
[Route("probe")]
public sealed class ProbeController : ControllerBase
{
    [HttpPost]
    public string Post([FromBody] Payload request) => request.Name;
}
