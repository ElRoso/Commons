namespace ElRoso.Commons.Tests;

using ElRoso.Helpers;

public class EncryptionHelperTests
{
    // Shared instance — BCrypt work factor is paid once per class, not once per test.
    // Instancia compartida — el work factor de BCrypt se paga una vez por clase, no por test.
    private static readonly EncryptionHelper Sut = new(workFactor: 4); // factor 4 for fast tests

    [Fact]
    public void HashPassword_ReturnsNonEmptyHash()
    {
        var hash = Sut.HashPassword("MyPassword1!");

        Assert.False(string.IsNullOrWhiteSpace(hash));
    }

    [Fact]
    public void HashPassword_ReturnsBCryptFormattedHash()
    {
        var hash = Sut.HashPassword("MyPassword1!");

        // BCrypt hashes always start with $2
        Assert.StartsWith("$2", hash);
    }

    [Fact]
    public void HashPassword_TwoCallsProduceDifferentHashes()
    {
        // BCrypt embeds a random salt — same password must not produce the same hash.
        // BCrypt embebe un salt aleatorio — la misma contraseña no debe producir el mismo hash.
        var hash1 = Sut.HashPassword("SamePassword1!");
        var hash2 = Sut.HashPassword("SamePassword1!");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyPassword_ReturnsTrueForCorrectPassword()
    {
        var hash = Sut.HashPassword("CorrectPass1!");

        Assert.True(Sut.VerifyPassword("CorrectPass1!", hash));
    }

    [Fact]
    public void VerifyPassword_ReturnsFalseForWrongPassword()
    {
        var hash = Sut.HashPassword("OriginalPass1!");

        Assert.False(Sut.VerifyPassword("WrongPass1!", hash));
    }

    [Fact]
    public void VerifyPassword_ReturnsFalseForEmptyPassword()
    {
        var hash = Sut.HashPassword("SomePass1!");

        Assert.False(Sut.VerifyPassword(string.Empty, hash));
    }

    [Fact]
    public void VerifyPassword_ReturnsFalseForEmptyHash()
    {
        Assert.False(Sut.VerifyPassword("SomePass1!", string.Empty));
    }

    // Guard tests / Tests de guardia

    [Fact]
    public void HashPassword_NullPassword_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Sut.HashPassword(null!));
    }

    [Fact]
    public void HashPassword_WhitespacePassword_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Sut.HashPassword("   "));
    }

    [Fact]
    public void Constructor_WorkFactorBelowMinimum_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EncryptionHelper(workFactor: 3));
    }

    [Fact]
    public void Constructor_WorkFactorAboveMaximum_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EncryptionHelper(workFactor: 32));
    }

    [Fact]
    public void Constructor_DefaultWorkFactor_DoesNotThrow()
    {
        // Default work factor (12) must be accepted without exception.
        // El work factor por defecto (12) debe aceptarse sin excepción.
        var ex = Record.Exception(() => new EncryptionHelper());
        Assert.Null(ex);
    }
}
