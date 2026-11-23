using ElRoso.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.AddElRosoConfiguration(args);
builder.AddElRosoSerilog();

builder.Services.AddElRosoForwardedHeaders(builder.Configuration);
builder.Services.AddEncryptionHelper(builder.Configuration);
builder.Services.AddMemoryCacheCustom();
builder.Services.AddHealthChecks();
builder.Services.AddExceptionHandlerMiddleware();
builder.Services.AddCurrentUserMiddleware();
builder.Services.AddControllersCustom(builder.Configuration);

var app = builder.Build();

app.UseElRosoForwardedHeaders();
app.UseExceptionHandlerMiddleware();
app.UseRouting();
app.UseCurrentUserMiddleware();

app.MapControllers();
app.MapHealthChecksCustom();
app.MapElRosoInfo(app.Environment);

app.Run();
