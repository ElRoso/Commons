// <copyright file="Argon2idPasswordHasher.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Argon2;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BCryptNet = BCrypt.Net.BCrypt;
using SaltParseException = BCrypt.Net.SaltParseException;
using ElRoso.Helpers;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

/// <summary>
/// Hash de contraseñas con Argon2id en formato PHC, con verificación de hashes BCrypt y re-hash al verificar.
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    /// <summary>Tope de memoria aceptado al leer un hash guardado.</summary>
    internal const int MaxMemoryKiB = 1_048_576;

    /// <summary>Tope de pasadas aceptado al leer un hash guardado.</summary>
    internal const int MaxIterations = 16;

    /// <summary>Tope de líneas aceptado al leer un hash guardado.</summary>
    internal const int MaxParallelism = 16;

    private const string Argon2idPrefix = "$argon2id$";
    private const string CurrentVersion = "v=19";
    private const string BcryptPrefix2a = "$2a$";
    private const string BcryptPrefix2b = "$2b$";
    private const string BcryptPrefix2x = "$2x$";
    private const string BcryptPrefix2y = "$2y$";

    private readonly Argon2idOptions options;

    /// <summary>
    /// Inicializa el hasher con los parámetros indicados.
    /// </summary>
    /// <param name="options">Parámetros de Argon2id; si se omite, se usan los valores por defecto.</param>
    /// <exception cref="ArgumentOutOfRangeException">Cuando algún parámetro está fuera de rango.</exception>
    public Argon2idPasswordHasher(Argon2idOptions? options = null)
    {
        this.options = options ?? new Argon2idOptions();
        Validate(this.options);
    }

    /// <summary>
    /// Calcula el tag Argon2id crudo con salt, secreto y datos asociados explícitos.
    /// </summary>
    internal static byte[] ComputeTag(
        byte[] password,
        byte[] salt,
        byte[]? secret,
        byte[]? associatedData,
        int memoryKiB,
        int iterations,
        int parallelism,
        int outputLength)
    {
        var builder = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
            .WithVersion(Argon2Parameters.Version13)
            .WithMemoryAsKB(memoryKiB)
            .WithIterations(iterations)
            .WithParallelism(parallelism)
            .WithSalt(salt);

        if (secret is not null)
        {
            builder.WithSecret(secret);
        }

        if (associatedData is not null)
        {
            builder.WithAdditional(associatedData);
        }

        var generator = new Argon2BytesGenerator();
        generator.Init(builder.Build());

        var output = new byte[outputLength];
        generator.GenerateBytes(password, output);

        return output;
    }

    /// <inheritdoc />
    public string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("La contraseña no puede ser nula ni vacía.", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(this.options.SaltLength);
        return this.HashWithSalt(password, salt);
    }

    /// <inheritdoc />
    public PasswordVerification Verify(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
        {
            return default;
        }

        if (storedHash.StartsWith(Argon2idPrefix, StringComparison.Ordinal))
        {
            return this.VerifyArgon2id(password, storedHash);
        }

        if (IsBcryptHash(storedHash))
        {
            return this.VerifyBcrypt(password, storedHash);
        }

        return default;
    }

    private static bool IsBcryptHash(string storedHash) =>
        storedHash.StartsWith(BcryptPrefix2a, StringComparison.Ordinal)
        || storedHash.StartsWith(BcryptPrefix2b, StringComparison.Ordinal)
        || storedHash.StartsWith(BcryptPrefix2x, StringComparison.Ordinal)
        || storedHash.StartsWith(BcryptPrefix2y, StringComparison.Ordinal);

    private static void Validate(Argon2idOptions options)
    {
        if (options.Parallelism < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Parallelism), options.Parallelism, "Parallelism debe ser mayor o igual a 1.");
        }

        if (options.MemoryKiB < 8 * options.Parallelism)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MemoryKiB), options.MemoryKiB, "MemoryKiB debe ser al menos 8 veces Parallelism.");
        }

        if (options.Iterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Iterations), options.Iterations, "Iterations debe ser mayor o igual a 1.");
        }

        if (options.SaltLength < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(options.SaltLength), options.SaltLength, "SaltLength debe ser mayor o igual a 16.");
        }

        if (options.HashLength < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(options.HashLength), options.HashLength, "HashLength debe ser mayor o igual a 16.");
        }
    }

    // Codifica bytes en Base64 sin padding, como exige el formato PHC.
    private static string ToBase64NoPadding(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    // Restaura el padding que el formato PHC omite.
    private static byte[] FromBase64NoPadding(string value)
    {
        var padding = (4 - (value.Length % 4)) % 4;
        return Convert.FromBase64String(value.PadRight(value.Length + padding, '='));
    }

    private string HashWithSalt(string password, byte[] salt)
    {
        var tag = ComputeTag(
            Encoding.UTF8.GetBytes(password),
            salt,
            secret: null,
            associatedData: null,
            this.options.MemoryKiB,
            this.options.Iterations,
            this.options.Parallelism,
            this.options.HashLength);

        return FormattableString.Invariant(
            $"{Argon2idPrefix}{CurrentVersion}$m={this.options.MemoryKiB},t={this.options.Iterations},p={this.options.Parallelism}${ToBase64NoPadding(salt)}${ToBase64NoPadding(tag)}");
    }

    private PasswordVerification VerifyArgon2id(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 6 || parts[2] != CurrentVersion)
            {
                return default;
            }

            var parameters = ParseParameters(parts[3]);
            if (parameters is null)
            {
                return default;
            }

            var (memoryKiB, iterations, parallelism) = parameters.Value;
            var salt = FromBase64NoPadding(parts[4]);
            var expected = FromBase64NoPadding(parts[5]);

            var tag = ComputeTag(
                Encoding.UTF8.GetBytes(password),
                salt,
                secret: null,
                associatedData: null,
                memoryKiB,
                iterations,
                parallelism,
                expected.Length);

            if (!CryptographicOperations.FixedTimeEquals(tag, expected))
            {
                return default;
            }

            var needsRehash = memoryKiB < this.options.MemoryKiB
                || iterations < this.options.Iterations
                || parallelism != this.options.Parallelism;

            return new PasswordVerification(true, needsRehash ? this.Hash(password) : null);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
        {
            return default;
        }
    }

    // Devuelve null si los parámetros guardados están fuera de rango.
    private static (int MemoryKiB, int Iterations, int Parallelism)? ParseParameters(string value)
    {
        var values = value.Split(',');
        if (values.Length != 3)
        {
            return null;
        }

        if (!TryParseParameter(values[0], "m=", out var memoryKiB)
            || !TryParseParameter(values[1], "t=", out var iterations)
            || !TryParseParameter(values[2], "p=", out var parallelism))
        {
            return null;
        }

        if (memoryKiB > MaxMemoryKiB || iterations > MaxIterations || parallelism > MaxParallelism)
        {
            return null;
        }

        if (parallelism < 1 || iterations < 1 || memoryKiB < 8 * parallelism)
        {
            return null;
        }

        return (memoryKiB, iterations, parallelism);
    }

    private static bool TryParseParameter(string value, string prefix, out int parsed)
    {
        parsed = 0;
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(value.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out parsed);
    }

    private PasswordVerification VerifyBcrypt(string password, string storedHash)
    {
        try
        {
            if (!BCryptNet.Verify(password, storedHash))
            {
                return default;
            }

            return new PasswordVerification(true, this.Hash(password));
        }
        catch (SaltParseException)
        {
            return default;
        }
        catch (ArgumentException)
        {
            return default;
        }
    }
}
