namespace ElRoso.Commons.Tests;

using System.Text.RegularExpressions;
using ElRoso.Argon2;
using ElRoso.DependencyInjection;
using ElRoso.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class Argon2idPasswordHasherTests
{
    private const string Password = "contrasenia-de-prueba";

    private const string ReferenceHash = "$argon2id$v=19$m=1024,t=2,p=1$c2FsdHNhbHRzYWx0c2FsdA$3BBwWZdUGiwYYWK9260Rv+4Z1cuHnyqIX7ufDBLsfHc";

    private static readonly Argon2idPasswordHasher Sut = new(new Argon2idOptions { MemoryKiB = 1024, Iterations = 1 });

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    [Fact]
    public void Compute_WithRfc9106Vector_ReturnsTheRfcTag()
    {
        var password = Enumerable.Repeat((byte)0x01, 32).ToArray();
        var salt = Enumerable.Repeat((byte)0x02, 16).ToArray();
        var secret = Enumerable.Repeat((byte)0x03, 8).ToArray();
        var associatedData = Enumerable.Repeat((byte)0x04, 12).ToArray();
        var expected = Convert.FromHexString("0d640df58d78766c08c037a34a8b53c9d01ef0452d75b65eb52520e96b01e659");

        var tag = Argon2idPasswordHasher.ComputeTag(
            password,
            salt,
            secret,
            associatedData,
            memoryKiB: 32,
            iterations: 3,
            parallelism: 4,
            outputLength: 32);

        Assert.Equal(expected, tag);
    }

    [Fact]
    public void Verify_WithReferenceImplementationHash_Succeeds()
    {
        var hasher = new Argon2idPasswordHasher(new Argon2idOptions { MemoryKiB = 1024, Iterations = 2 });

        var result = hasher.Verify(Password, ReferenceHash);

        Assert.True(result.Succeeded);
        Assert.False(result.NeedsRehash);
    }

    [Fact]
    public void Hash_MatchesPhcFormat()
    {
        var hash = Sut.Hash(Password);

        Assert.Matches(
            new Regex(@"^\$argon2id\$v=19\$m=\d+,t=\d+,p=\d+\$[A-Za-z0-9+/]{22}\$[A-Za-z0-9+/]{43}$"),
            hash);
    }

    [Fact]
    public void Hash_TwoCallsDiffer_AndBothVerify()
    {
        var first = Sut.Hash(Password);
        var second = Sut.Hash(Password);

        Assert.NotEqual(first, second);
        Assert.True(Sut.Verify(Password, first).Succeeded);
        Assert.True(Sut.Verify(Password, second).Succeeded);
    }

    [Fact]
    public void Hash_WithBlankPassword_Throws()
    {
        Assert.Throws<ArgumentException>(() => Sut.Hash(string.Empty));
        Assert.Throws<ArgumentException>(() => Sut.Hash("   "));
        Assert.Throws<ArgumentException>(() => Sut.Hash(null!));
    }

    [Fact]
    public void Verify_WithWrongPassword_FailsWithoutRehash()
    {
        var hash = Sut.Hash(Password);

        var result = Sut.Verify("otra-contrasenia", hash);

        Assert.False(result.Succeeded);
        Assert.False(result.NeedsRehash);
    }

    [Fact]
    public void Verify_WithBlankPassword_Fails()
    {
        var hash = Sut.Hash(Password);

        Assert.False(Sut.Verify(string.Empty, hash).Succeeded);
        Assert.False(Sut.Verify(null!, hash).Succeeded);
    }

    [Fact]
    public void Verify_WithCorrectBCryptHash_SucceedsAndReturnsArgon2idRehash()
    {
        var bcryptHash = new EncryptionHelper(workFactor: 4).HashPassword(Password);

        var result = Sut.Verify(Password, bcryptHash);

        Assert.True(result.Succeeded);
        Assert.True(result.NeedsRehash);
        Assert.StartsWith("$argon2id$", result.RehashedHash);
    }

    [Fact]
    public void Verify_WithBCryptHashRehash_VerifiesWithoutAnotherRehash()
    {
        var bcryptHash = new EncryptionHelper(workFactor: 4).HashPassword(Password);
        var rehash = Sut.Verify(Password, bcryptHash).RehashedHash!;

        var result = Sut.Verify(Password, rehash);

        Assert.True(result.Succeeded);
        Assert.False(result.NeedsRehash);
    }

    [Fact]
    public void Verify_WithWrongBCryptPassword_FailsWithoutRehash()
    {
        var bcryptHash = new EncryptionHelper(workFactor: 4).HashPassword(Password);

        var result = Sut.Verify("otra-contrasenia", bcryptHash);

        Assert.False(result.Succeeded);
        Assert.False(result.NeedsRehash);
    }

    [Fact]
    public void Verify_WithBCryptVariantPrefixes_Succeeds()
    {
        var bcryptHash = new EncryptionHelper(workFactor: 4).HashPassword(Password);

        foreach (var prefix in new[] { "$2a$", "$2b$", "$2y$" })
        {
            var variant = prefix + bcryptHash[4..];
            Assert.True(Sut.Verify(Password, variant).Succeeded, prefix);
        }
    }

    [Fact]
    public void Verify_WithLowerArgon2Parameters_SucceedsAndReturnsRehash()
    {
        var weak = new Argon2idPasswordHasher(new Argon2idOptions { MemoryKiB = 512, Iterations = 1 });
        var hash = weak.Hash(Password);

        var result = Sut.Verify(Password, hash);

        Assert.True(result.Succeeded);
        Assert.True(result.NeedsRehash);
        Assert.True(Sut.Verify(Password, result.RehashedHash!).Succeeded);
    }

    [Fact]
    public void Verify_WithSameParameters_ReturnsNoRehash()
    {
        var hash = Sut.Hash(Password);

        var result = Sut.Verify(Password, hash);

        Assert.True(result.Succeeded);
        Assert.False(result.NeedsRehash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("texto")]
    [InlineData("$argon2i$v=19$m=1024,t=1,p=1$c2FsdA$aGFzaA")]
    [InlineData("$argon2id$v=19$m=abc,t=1,p=1$c2FsdA$aGFzaA")]
    [InlineData("$argon2id$v=19$m=1024,t=1,p=1$!!!!$aGFzaA")]
    [InlineData("$argon2id$v=19$m=1024,t=1,p=1$c2FsdA$!!!!")]
    [InlineData("$argon2id$v=16$m=1024,t=1,p=1$c2FsdA$aGFzaA")]
    public void Verify_WithUnreadableHash_FailsWithoutThrowing(string storedHash)
    {
        var result = Sut.Verify(Password, storedHash);

        Assert.False(result.Succeeded);
        Assert.False(result.NeedsRehash);
    }

    [Theory]
    [InlineData("$argon2id$v=19$m=1048577,t=1,p=1$c2FsdHNhbHRzYWx0c2FsdA$3BBwWZdUGiwYYWK9260Rv+4Z1cuHnyqIX7ufDBLsfHc")]
    [InlineData("$argon2id$v=19$m=1024,t=17,p=1$c2FsdHNhbHRzYWx0c2FsdA$3BBwWZdUGiwYYWK9260Rv+4Z1cuHnyqIX7ufDBLsfHc")]
    [InlineData("$argon2id$v=19$m=1024,t=1,p=17$c2FsdHNhbHRzYWx0c2FsdA$3BBwWZdUGiwYYWK9260Rv+4Z1cuHnyqIX7ufDBLsfHc")]
    public void Verify_WithStoredParametersAboveCap_FailsWithoutThrowing(string storedHash)
    {
        var result = Sut.Verify(Password, storedHash);

        Assert.False(result.Succeeded);
        Assert.False(result.NeedsRehash);
    }

    [Fact]
    public void AddArgon2PasswordHasher_ReplacesIPasswordHasher_InBothOrders()
    {
        var coreFirst = new ServiceCollection();
        coreFirst.AddEncryptionHelper(BuildConfiguration());
        coreFirst.AddArgon2PasswordHasher(BuildConfiguration(("PasswordHashing:Argon2:MemoryKiB", "1024"), ("PasswordHashing:Argon2:Iterations", "1")));
        using var firstProvider = coreFirst.BuildServiceProvider();
        Assert.IsType<Argon2idPasswordHasher>(firstProvider.GetRequiredService<IPasswordHasher>());

        var argon2First = new ServiceCollection();
        argon2First.AddArgon2PasswordHasher(BuildConfiguration(("PasswordHashing:Argon2:MemoryKiB", "1024"), ("PasswordHashing:Argon2:Iterations", "1")));
        argon2First.AddEncryptionHelper(BuildConfiguration());
        using var secondProvider = argon2First.BuildServiceProvider();
        Assert.IsType<Argon2idPasswordHasher>(secondProvider.GetRequiredService<IPasswordHasher>());
    }

    [Fact]
    public void AddArgon2PasswordHasher_KeepsEncryptionHelperResolvable()
    {
        var services = new ServiceCollection();
        services.AddEncryptionHelper(BuildConfiguration());
        services.AddArgon2PasswordHasher(BuildConfiguration());

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<EncryptionHelper>());
    }

    [Fact]
    public void AddArgon2PasswordHasher_AppliesConfiguredParameters()
    {
        var services = new ServiceCollection();
        services.AddArgon2PasswordHasher(BuildConfiguration(
            ("PasswordHashing:Argon2:MemoryKiB", "1024"),
            ("PasswordHashing:Argon2:Iterations", "1"),
            ("PasswordHashing:Argon2:Parallelism", "2")));

        using var provider = services.BuildServiceProvider();
        var hasher = provider.GetRequiredService<IPasswordHasher>();

        Assert.StartsWith("$argon2id$v=19$m=1024,t=1,p=2$", hasher.Hash(Password));
    }
}
