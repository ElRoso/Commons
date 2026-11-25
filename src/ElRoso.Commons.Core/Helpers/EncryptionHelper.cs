// <copyright file="EncryptionHelper.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Helpers
{
    using BCrypt.Net;

    /// <summary>
    /// Provides secure password hashing and verification using BCrypt.
    /// The work factor controls CPU cost — higher means slower hashing and stronger brute-force resistance.
    /// Proveedor de hash y verificación de contraseñas usando BCrypt.
    /// El work factor controla el costo de CPU — mayor significa hash más lento y mayor resistencia a fuerza bruta.
    /// </summary>
    public class EncryptionHelper : IPasswordHasher
    {
        /// <summary>Minimum allowed BCrypt work factor. / Factor de trabajo mínimo permitido.</summary>
        private const int MinWorkFactor = 4;

        /// <summary>Maximum allowed BCrypt work factor. / Factor de trabajo máximo permitido.</summary>
        private const int MaxWorkFactor = 31;

        private readonly int workFactor;

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptionHelper"/> class.
        /// Initializes a new instance of <see cref="EncryptionHelper"/>.
        /// Inicializa una nueva instancia de <see cref="EncryptionHelper"/>.
        /// </summary>
        /// <param name="workFactor">
        /// BCrypt work factor (cost). Default is 12, which provides ~250ms hashing time.
        /// Factor de trabajo de BCrypt. Por defecto es 12, que provee ~250ms de tiempo de hash.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">When work factor is outside [4, 31].</exception>
        public EncryptionHelper(int workFactor = 12)
        {
            if (workFactor < MinWorkFactor || workFactor > MaxWorkFactor)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(workFactor),
                    $"Work factor must be between {MinWorkFactor} and {MaxWorkFactor}. / El factor de trabajo debe estar entre {MinWorkFactor} y {MaxWorkFactor}.");
            }

            this.workFactor = workFactor;
        }

        /// <summary>
        /// Hashes a plain-text password using BCrypt with the configured work factor.
        /// Hashea una contraseña en texto plano usando BCrypt con el factor de trabajo configurado.
        /// </summary>
        /// <param name="password">The plain-text password to hash. / La contraseña en texto plano a hashear.</param>
        /// <returns>A BCrypt hash string (e.g., "$2a$12$..."). Safe to store in the database.</returns>
        /// <exception cref="ArgumentException">When password is null or whitespace.</exception>
        public string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password must not be null or whitespace. / La contraseña no puede ser nula ni vacía.", nameof(password));
            }

            return BCrypt.HashPassword(password, this.workFactor);
        }

        /// <summary>
        /// Verifies a plain-text password against a BCrypt hash.
        /// Verifica una contraseña en texto plano contra un hash BCrypt.
        /// </summary>
        /// <param name="password">The plain-text password to verify. / La contraseña en texto plano a verificar.</param>
        /// <param name="hash">The BCrypt hash to compare against. / El hash BCrypt contra el que comparar.</param>
        /// <returns><c>true</c> if the password matches the hash; otherwise <c>false</c>.</returns>
        public bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }

            return BCrypt.Verify(password, hash);
        }

        /// <summary>
        /// Hashea una contraseña en texto plano usando BCrypt con el factor de trabajo configurado.
        /// </summary>
        /// <param name="password">La contraseña en texto plano a hashear.</param>
        /// <returns>El hash BCrypt listo para guardar.</returns>
        public string Hash(string password) => this.HashPassword(password);

        /// <summary>
        /// Verifica una contraseña contra un hash BCrypt y devuelve el hash nuevo si el factor de trabajo subió.
        /// </summary>
        /// <param name="password">La contraseña en texto plano a verificar.</param>
        /// <param name="storedHash">El hash BCrypt guardado.</param>
        /// <returns>El resultado de la verificación y el hash nuevo cuando corresponde re-hashear.</returns>
        public PasswordVerification Verify(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
            {
                return default;
            }

            try
            {
                if (!BCrypt.Verify(password, storedHash))
                {
                    return default;
                }

                var rehashed = BCrypt.PasswordNeedsRehash(storedHash, this.workFactor)
                    ? this.HashPassword(password)
                    : null;

                return new PasswordVerification(true, rehashed);
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
}
