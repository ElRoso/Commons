namespace ElRoso.Commons.Tests;

using ElRoso.Argon2;

public class Argon2idOptionsTests
{
    [Fact]
    public void Defaults_UseOwaspParameters()
    {
        var options = new Argon2idOptions();

        Assert.Equal(19456, options.MemoryKiB);
        Assert.Equal(2, options.Iterations);
        Assert.Equal(1, options.Parallelism);
        Assert.Equal(16, options.SaltLength);
        Assert.Equal(32, options.HashLength);
    }

    [Theory]
    [InlineData(19456, 2, 0, 16, 32, "Parallelism")]
    [InlineData(1024, 2, 200, 16, 32, "MemoryKiB")]
    [InlineData(19456, 0, 1, 16, 32, "Iterations")]
    [InlineData(19456, 2, 1, 15, 32, "SaltLength")]
    [InlineData(19456, 2, 1, 16, 15, "HashLength")]
    public void Options_Invalid_ThrowsArgumentOutOfRange(
        int memoryKiB,
        int iterations,
        int parallelism,
        int saltLength,
        int hashLength,
        string parameterName)
    {
        var options = new Argon2idOptions
        {
            MemoryKiB = memoryKiB,
            Iterations = iterations,
            Parallelism = parallelism,
            SaltLength = saltLength,
            HashLength = hashLength,
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Argon2idPasswordHasher(options));

        Assert.Contains(parameterName, exception.Message, StringComparison.Ordinal);
    }
}
