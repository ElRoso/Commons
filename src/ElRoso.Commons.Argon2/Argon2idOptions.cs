// <copyright file="Argon2idOptions.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Argon2;

/// <summary>
/// Parámetros de Argon2id. Los valores por defecto son los recomendados por OWASP.
/// </summary>
public sealed record Argon2idOptions
{
    /// <summary>Memoria en KiB.</summary>
    public int MemoryKiB { get; init; } = 19456;

    /// <summary>Cantidad de pasadas.</summary>
    public int Iterations { get; init; } = 2;

    /// <summary>Líneas de ejecución en paralelo.</summary>
    public int Parallelism { get; init; } = 1;

    /// <summary>Largo del salt en bytes.</summary>
    public int SaltLength { get; init; } = 16;

    /// <summary>Largo del hash en bytes.</summary>
    public int HashLength { get; init; } = 32;
}
