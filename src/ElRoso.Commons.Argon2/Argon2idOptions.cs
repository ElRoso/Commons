// <copyright file="Argon2idOptions.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Argon2;

/// <summary>
/// Define los parámetros usados para hashear con Argon2id.
/// </summary>
public sealed record Argon2idOptions
{
    /// <summary>
    /// Gets the memory size in KiB.
    /// Memoria usada por el hash, en KiB.
    /// </summary>
    public int MemoryKiB { get; init; } = 19456;

    /// <summary>
    /// Gets the number of passes.
    /// Cantidad de pasadas de Argon2id.
    /// </summary>
    public int Iterations { get; init; } = 2;

    /// <summary>
    /// Gets the number of parallel lanes.
    /// Cantidad de carriles de ejecución en paralelo.
    /// </summary>
    public int Parallelism { get; init; } = 1;

    /// <summary>
    /// Gets the salt length in bytes.
    /// Longitud del salt, en bytes.
    /// </summary>
    public int SaltLength { get; init; } = 16;

    /// <summary>
    /// Gets the hash length in bytes.
    /// Longitud del hash, en bytes.
    /// </summary>
    public int HashLength { get; init; } = 32;
}
