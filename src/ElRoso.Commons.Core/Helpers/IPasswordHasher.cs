// <copyright file="IPasswordHasher.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Helpers
{
    /// <summary>
    /// Contrato de hash y verificación de contraseñas.
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// Hashea una contraseña en texto plano.
        /// </summary>
        /// <param name="password">Contraseña en texto plano.</param>
        /// <returns>El hash listo para guardar.</returns>
        string Hash(string password);

        /// <summary>
        /// Verifica una contraseña contra un hash guardado y, si corresponde, devuelve el hash a persistir.
        /// </summary>
        /// <param name="password">Contraseña en texto plano.</param>
        /// <param name="storedHash">Hash guardado.</param>
        /// <returns>El resultado de la verificación y el hash nuevo cuando hace falta re-hashear.</returns>
        PasswordVerification Verify(string password, string storedHash);
    }
}
