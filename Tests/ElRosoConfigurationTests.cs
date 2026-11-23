namespace ElRoso.Commons.Tests;

using ElRoso.Commons;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;

public class ElRosoConfigurationTests
{
    [Fact]
    public void AddConfiguration_InDevelopment_LoadsTheUserSecretsOfTheAppBeforeTheEnvironmentVariables()
    {
        var sources = SourcesFor(Environments.Development);

        var secrets = sources.FindIndex(IsUserSecrets);
        var environmentVariables = sources.FindIndex(s => s is EnvironmentVariablesConfigurationSource);
        Assert.True(secrets >= 0, "Development tiene que cargar user-secrets.");
        Assert.True(secrets < environmentVariables, "Las variables de entorno tienen que poder pisar user-secrets.");
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void AddConfiguration_OutsideDevelopment_DoesNotLoadUserSecrets(string environment)
    {
        Assert.DoesNotContain(SourcesFor(environment), IsUserSecrets);
    }

    private static List<IConfigurationSource> SourcesFor(string environment)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new()
        {
            EnvironmentName = environment,
            ApplicationName = typeof(ElRosoConfigurationTests).Assembly.GetName().Name,
        });

        builder.AddConfiguration([]);

        return [.. builder.Configuration.Sources];
    }

    private static bool IsUserSecrets(IConfigurationSource source) =>
        source is JsonConfigurationSource { Path: "secrets.json" };
}
