using Asp.Versioning;
using ElRoso.DependencyInjection;
using ElRoso.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiVersioningCustom(builder.Configuration);
builder.Services.AddElRosoOpenApi(builder.Configuration, OpenApiAuthSchemes.Bearer);

var app = builder.Build();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .HasDeprecatedApiVersion(new ApiVersion(2, 0))
    .ReportApiVersions()
    .Build();

app.MapGet("/v{version:apiVersion}/sample", () => new SampleResponse("1.0", "Muestra v1"))
    .WithApiVersionSet(apiVersionSet)
    .HasApiVersion(new ApiVersion(1, 0))
    .ProducesProblem(404);

app.MapGet("/v{version:apiVersion}/sample", () => new SampleResponse("2.0", "Muestra v2"))
    .WithApiVersionSet(apiVersionSet)
    .HasDeprecatedApiVersion(new ApiVersion(2, 0))
    .ProducesProblem(404);

app.MapElRosoOpenApi();

app.Run();

namespace ElRoso.OpenApi
{
    public sealed record SampleResponse(string Version, string Message);
}
