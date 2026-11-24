// <copyright file="OpenApiAuthSchemes.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.OpenApi;

/// <summary>
/// Esquemas de seguridad a publicar en los documentos OpenAPI generados.
/// </summary>
[Flags]
public enum OpenApiAuthSchemes
{
    /// <summary>No se publica ningún esquema.</summary>
    None = 0,

    /// <summary>Esquema HTTP bearer con formato JWT.</summary>
    Bearer = 1,

    /// <summary>Esquema HTTP basic.</summary>
    Basic = 2,
}
