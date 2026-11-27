namespace ElRoso.Commons.Tests;

using ElRoso.DependencyInjection;
using ElRoso.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class PasswordHasherTests
{
    private static readonly EncryptionHelper Sut = new(workFactor: 4);

    private static IConfiguration BuildConfiguration(int workFactor) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:WorkFactor"] = workFactor.ToString(),
            })
            .Build();

    [Fact]
    public void EncryptionHelper_IsAPasswordHasher()
    {
        Assert.IsAssignableFrom<IPasswordHasher>(Sut);
    }

    [Fact]
    public void Hash_ReturnsABCryptHashThatVerifies()
    {
        var hash = Sut.Hash("MyPassword1!");

        Assert.StartsWith("$2", hash);
        Assert.True(Sut.Verify("MyPassword1!", hash).Succeeded);
    }

    [Fact]
    public void Verify_WithCorrectPasswordAndSameWorkFactor_SucceedsWithoutRehash()
    {
        var hash = Sut.Hash("MyPassword1!");

        var result = Sut.Verify("MyPassword1!", hash);

        Assert.True(result.Succeeded);
        Assert.False(result.NeedsRehash);
        Assert.Null(result.RehashedHash);
    }

    [Fact]
    public void Verify_WithLowerWorkFactorHash_SucceedsAndReturnsVerifiableRehash()
    {
        var hash = Sut.Hash("MyPassword1!");
        var stronger = new EncryptionHelper(workFactor: 5);

        var result = stronger.Verify("MyPassword1!", hash);

        Assert.True(result.Succeeded);
        Assert.True(result.NeedsRehash);
        Assert.NotNull(result.RehashedHash);
        Assert.True(stronger.Verify("MyPassword1!", result.RehashedHash!).Succeeded);
        Assert.False(stronger.Verify("MyPassword1!", result.RehashedHash!).NeedsRehash);
    }

    [Fact]
    public void Verify_WithWrongPassword_FailsWithoutRehash()
    {
        var hash = Sut.Hash("MyPassword1!");

        var result = Sut.Verify("OtherPassword1!", hash);

        Assert.False(result.Succeeded);
        Assert.False(result.NeedsRehash);
    }

    [Fact]
    public void Verify_WithBlankPassword_Fails()
    {
        var hash = Sut.Hash("MyPassword1!");

        Assert.False(Sut.Verify(string.Empty, hash).Succeeded);
        Assert.False(Sut.Verify("   ", hash).Succeeded);
        Assert.False(Sut.Verify(null!, hash).Succeeded);
    }

    [Fact]
    public void Verify_WithUnreadableHash_FailsWithoutThrowing()
    {
        Assert.False(Sut.Verify("MyPassword1!", "texto").Succeeded);
    }

    [Fact]
    public void Verify_WithArgon2Hash_FailsWithoutThrowing()
    {
        Assert.False(Sut.Verify("MyPassword1!", "$argon2id$v=19$m=1024,t=1,p=1$c2FsdA$aGFzaA").Succeeded);
    }

    [Fact]
    public void AddEncryptionHelper_ResolvesIPasswordHasherAsTheSameSingleton()
    {
        var services = new ServiceCollection();
        services.AddEncryptionHelper(BuildConfiguration(workFactor: 4));

        using var provider = services.BuildServiceProvider();
        var helper = provider.GetRequiredService<EncryptionHelper>();
        var hasher = provider.GetRequiredService<IPasswordHasher>();

        Assert.Same(helper, hasher);
    }
}
