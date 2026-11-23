// <copyright file="CustomRandom.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Commons
{
    using System;
    using System.Security.Cryptography;

    /// <summary>
    /// EN: Random values for security-adjacent uses — temporary passwords, one-time codes, object
    ///     keys. Every member draws from <see cref="RandomNumberGenerator"/>, never from
    ///     <see cref="Random"/>: a pseudo-random generator is predictable, so whoever observes a few
    ///     outputs can derive the rest. Until 2026-08-05 this class used <c>Random.Shared</c>, which
    ///     made every value it produced guessable.
    /// ES: Valores aleatorios para usos cercanos a seguridad — contraseñas temporales, códigos de un
    ///     solo uso, keys de objetos. Todo miembro sale de <see cref="RandomNumberGenerator"/>,
    ///     nunca de <see cref="Random"/>: un generador pseudoaleatorio es predecible, así que quien
    ///     observe algunas salidas puede derivar el resto. Hasta el 2026-08-05 esta clase usaba
    ///     <c>Random.Shared</c>, lo que hacía adivinable todo valor que producía.
    /// </summary>
    public class CustomRandom
    {
        /// <summary>Minimum length accepted by <see cref="Password(int)"/>. / Longitud mínima aceptada.</summary>
        private const int MinPasswordLength = 8;

        private const string UpperCaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private const string LowerCaseLetters = "abcdefghijklmnopqrstuvwxyz";

        // EN: Password alphabet without look-alikes (0/O, 1/l/I). A generated password gets read off
        //     a screen or a chat message and typed by hand — an ambiguous character turns into a
        //     support call, and the entropy given up is negligible.
        // ES: Alfabeto de contraseñas sin caracteres que se confunden (0/O, 1/l/I). Una contraseña
        //     generada se lee de una pantalla o un mensaje y se tipea a mano — un carácter ambiguo
        //     termina en un llamado a soporte, y la entropía que se resigna es despreciable.
        private const string PasswordAlphabet =
            "ABCDEFGHJKLMNPQRSTUVWXYZ" + "abcdefghijkmnopqrstuvwxyz" + "23456789";

        /// <summary>
        /// EN: Returns a value in [<paramref name="minValue"/>, <paramref name="maxValue"/>).
        /// ES: Devuelve un valor en [<paramref name="minValue"/>, <paramref name="maxValue"/>).
        /// </summary>
        /// <param name="minValue">Inclusive lower bound. / Límite inferior inclusivo.</param>
        /// <param name="maxValue">Exclusive upper bound. / Límite superior exclusivo.</param>
        /// <returns>A cryptographically strong value inside the range.</returns>
        /// <exception cref="ArgumentOutOfRangeException">When the range is empty. / Cuando el rango es vacío.</exception>
        public static int Number(int minValue, int maxValue)
        {
            if (maxValue <= minValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxValue),
                    $"maxValue ({maxValue}) must be greater than minValue ({minValue}). / maxValue debe ser mayor que minValue.");
            }

            return RandomNumberGenerator.GetInt32(minValue, maxValue);
        }

        /// <summary>
        /// EN: Returns a random string of letters with the requested length.
        /// ES: Devuelve una cadena aleatoria de letras con la longitud pedida.
        /// </summary>
        /// <param name="size">Number of characters. / Cantidad de caracteres.</param>
        /// <param name="lowerCase">When true, returns lowercase letters. / Si es true, minúsculas.</param>
        /// <returns>A cryptographically strong string of letters.</returns>
        /// <exception cref="ArgumentOutOfRangeException">When size is not positive. / Cuando size no es positivo.</exception>
        public static string String(int size, bool lowerCase = false)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            return RandomNumberGenerator.GetString(lowerCase ? LowerCaseLetters : UpperCaseLetters, size);
        }

        /// <summary>
        /// EN: Generates a password of <paramref name="length"/> characters over an unambiguous
        ///     alphabet (58 symbols ≈ 5.86 bits per character, so 16 characters ≈ 93 bits). This is
        ///     the overload to use for anything that protects something.
        /// ES: Genera una contraseña de <paramref name="length"/> caracteres sobre un alfabeto sin
        ///     ambigüedades (58 símbolos ≈ 5,86 bits por carácter, o sea 16 caracteres ≈ 93 bits).
        ///     Ésta es la sobrecarga a usar para cualquier cosa que proteja algo.
        /// </summary>
        /// <param name="length">Password length, at least 8. / Longitud, mínimo 8.</param>
        /// <returns>A cryptographically strong password.</returns>
        /// <exception cref="ArgumentOutOfRangeException">When length is below the minimum. / Cuando la longitud es menor al mínimo.</exception>
        public static string Password(int length)
        {
            if (length < MinPasswordLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    $"Password length must be at least {MinPasswordLength}. / La longitud mínima de la contraseña es {MinPasswordLength}.");
            }

            return RandomNumberGenerator.GetString(PasswordAlphabet, length);
        }

        /// <summary>
        /// EN: Legacy shape — 4 lowercase letters, 4 digits, 2 uppercase letters. Its source of
        ///     randomness is cryptographic now, but 10 characters over that layout is roughly 38
        ///     bits of entropy: weak against an offline attack. Kept only so existing callers keep
        ///     compiling.
        /// ES: Forma histórica — 4 minúsculas, 4 dígitos, 2 mayúsculas. Su fuente de aleatoriedad ya
        ///     es criptográfica, pero 10 caracteres con ese formato son unos 38 bits de entropía:
        ///     débil frente a un ataque offline. Se mantiene solo para que los llamadores existentes
        ///     sigan compilando.
        /// </summary>
        /// <returns>A 10-character password with the legacy layout.</returns>
        [Obsolete("Use Password(int length) — the legacy layout gives ~38 bits of entropy. / Usar Password(int length): el formato histórico da ~38 bits de entropía.")]
        public static string Password()
        {
            return String(4, lowerCase: true) + Number(1000, 10000) + String(2);
        }
    }
}
