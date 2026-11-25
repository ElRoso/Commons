// <copyright file="PasswordVerification.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Helpers
{
    /// <summary>
    /// Resultado de verificar una contraseña.
    /// </summary>
    /// <param name="Succeeded">Indica si la contraseña coincide con el hash guardado.</param>
    /// <param name="RehashedHash">Hash nuevo a persistir, o <c>null</c> si no hace falta re-hashear.</param>
#pragma warning disable SA1313 // StyleCop 1.1.118 no reconoce los parámetros de un record posicional.
    public readonly record struct PasswordVerification(bool Succeeded, string? RehashedHash)
#pragma warning restore SA1313
    {
        /// <summary>
        /// Gets a value indicating whether the verification returned a new hash to store.
        /// ES: Indica si la verificación devolvió un hash nuevo para guardar.
        /// </summary>
        public bool NeedsRehash => this.RehashedHash is not null;
    }
}
