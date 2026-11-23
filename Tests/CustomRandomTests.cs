namespace ElRoso.Commons.Tests;

using ElRoso.Commons;

public class CustomRandomTests
{
    // EN: These values feed temporary passwords and one-time codes. A predictable generator
    //     (Random.Shared) lets an attacker who observed a few outputs derive the rest, so every
    //     member of this class must draw from RandomNumberGenerator. That property cannot be
    //     asserted directly from outside — what these tests do assert is the observable contract
    //     (range, length, alphabet) plus a distribution check that catches a generator that is
    //     stuck, constant or badly seeded.
    // ES: Estos valores alimentan contraseñas temporales y códigos de un solo uso. Un generador
    //     predecible (Random.Shared) le permite a un atacante que vio algunas salidas derivar el
    //     resto, así que todo miembro de esta clase tiene que salir de RandomNumberGenerator. Esa
    //     propiedad no se puede afirmar desde afuera — lo que sí verifican estos tests es el
    //     contrato observable (rango, longitud, alfabeto) más un chequeo de distribución que agarra
    //     un generador trabado, constante o mal sembrado.
    private const int DistributionSampleSize = 500;

    [Fact]
    public void Number_StaysWithinTheRequestedRange()
    {
        for (var i = 0; i < DistributionSampleSize; i++)
        {
            var value = CustomRandom.Number(10, 20);

            Assert.InRange(value, 10, 19);
        }
    }

    [Fact]
    public void Number_WhenRangeHasASingleValue_ReturnsIt()
    {
        Assert.Equal(7, CustomRandom.Number(7, 8));
    }

    [Fact]
    public void Number_ProducesMoreThanOneDistinctValue()
    {
        var values = Enumerable.Range(0, DistributionSampleSize)
            .Select(_ => CustomRandom.Number(0, 1000))
            .ToHashSet();

        // EN: A stuck or constant generator collapses to a handful of values.
        Assert.True(values.Count > 100, $"Expected a spread of values, got {values.Count} distinct.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(64)]
    public void String_ReturnsTheRequestedLength(int size)
    {
        Assert.Equal(size, CustomRandom.String(size).Length);
    }

    [Fact]
    public void String_WhenLowerCaseRequested_ReturnsOnlyLowerCaseLetters()
    {
        var value = CustomRandom.String(40, lowerCase: true);

        Assert.All(value, c => Assert.InRange(c, 'a', 'z'));
    }

    [Fact]
    public void String_WhenLowerCaseNotRequested_ReturnsOnlyUpperCaseLetters()
    {
        var value = CustomRandom.String(40);

        Assert.All(value, c => Assert.InRange(c, 'A', 'Z'));
    }

    [Fact]
    public void String_ProducesDifferentValuesAcrossCalls()
    {
        var values = Enumerable.Range(0, DistributionSampleSize)
            .Select(_ => CustomRandom.String(12))
            .ToHashSet();

        // EN: 12 letters give 26^12 combinations — repeats here would mean a broken generator.
        Assert.Equal(DistributionSampleSize, values.Count);
    }

    [Fact]
    public void Password_KeepsItsLegacyShape()
    {
        // EN: Regression guard for the parameterless overload: 4 lowercase + 4 digits + 2 uppercase.
        //     Its shape is preserved so existing callers keep working; its weakness is why the
        //     overload is obsolete in favour of Password(length).
        // ES: Guarda de regresión de la sobrecarga sin parámetros: 4 minúsculas + 4 dígitos + 2
        //     mayúsculas. Se preserva la forma para no romper llamadores; su debilidad es la razón
        //     por la que queda obsoleta frente a Password(length).
#pragma warning disable CS0618 // deliberately exercising the obsolete overload
        var password = CustomRandom.Password();
#pragma warning restore CS0618

        Assert.Equal(10, password.Length);
        Assert.All(password[..4], c => Assert.InRange(c, 'a', 'z'));
        Assert.All(password[4..8], c => Assert.InRange(c, '0', '9'));
        Assert.All(password[8..], c => Assert.InRange(c, 'A', 'Z'));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(32)]
    public void Password_WithLength_ReturnsThatManyCharacters(int length)
    {
        Assert.Equal(length, CustomRandom.Password(length).Length);
    }

    [Fact]
    public void Password_WithLength_UsesOnlyUnambiguousCharacters()
    {
        // EN: No 0/O nor 1/l/I — these get typed by a human reading them off a screen or a message.
        // ES: Sin 0/O ni 1/l/I — a esto lo tipea una persona leyéndolo de una pantalla o un mensaje.
        var password = CustomRandom.Password(200);

        Assert.All(password, c => Assert.DoesNotContain(c, "0O1lI"));
        Assert.All(password, c => Assert.True(char.IsLetterOrDigit(c), $"Unexpected character '{c}'."));
    }

    [Fact]
    public void Password_WithLength_ProducesDifferentValuesAcrossCalls()
    {
        var values = Enumerable.Range(0, DistributionSampleSize)
            .Select(_ => CustomRandom.Password(16))
            .ToHashSet();

        Assert.Equal(DistributionSampleSize, values.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(7)]
    public void Password_WithLengthBelowTheMinimum_Throws(int length)
    {
        // EN: A password shorter than 8 characters is not worth generating — fail loudly instead of
        //     handing out something that looks secure and is not.
        // ES: Una contraseña de menos de 8 caracteres no vale la pena generarla — fallar fuerte en
        //     vez de entregar algo que parece seguro y no lo es.
        Assert.Throws<ArgumentOutOfRangeException>(() => CustomRandom.Password(length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void String_WithNonPositiveSize_Throws(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CustomRandom.String(size));
    }

    [Fact]
    public void Number_WhenMaxIsNotGreaterThanMin_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CustomRandom.Number(5, 5));
    }
}
