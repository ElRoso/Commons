// <copyright file="ErrorType.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Outcomes
{
    /// <summary>
    /// Clasifica el origen de una falla.
    /// </summary>
    public enum ErrorType
    {
        /// <summary>
        /// Falla genérica de la operación.
        /// </summary>
        Failure,

        /// <summary>
        /// Falla por datos inválidos.
        /// </summary>
        Validation,

        /// <summary>
        /// Falla por recurso inexistente.
        /// </summary>
        NotFound,

        /// <summary>
        /// Falla por estado contradictorio del recurso.
        /// </summary>
        Conflict,

        /// <summary>
        /// Falla por falta de autenticación.
        /// </summary>
        Unauthorized,

        /// <summary>
        /// Falla por permisos insuficientes.
        /// </summary>
        Forbidden,
    }
}
