// <copyright file="ResultOfT.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Results
{
    using System;

    /// <summary>
    /// Representa el resultado de una operación que devuelve un valor.
    /// </summary>
    /// <typeparam name="T">Tipo del valor devuelto.</typeparam>
    public sealed class Result<T> : Result
    {
        private readonly T value;

        private Result(T value)
            : base(true, null)
        {
            this.value = value;
        }

        private Result(Error error)
            : base(false, error)
        {
            this.value = default!;
        }

        /// <summary>
        /// Gets the value of a successful result.
        /// ES: Valor del resultado cuando hay éxito.
        /// </summary>
        /// <exception cref="InvalidOperationException">El resultado es una falla.</exception>
        public T Value => this.IsSuccess
            ? this.value
            : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

        /// <summary>
        /// Convierte un valor en un resultado con éxito.
        /// </summary>
        /// <param name="value">Valor del resultado.</param>
        public static implicit operator Result<T>(T value) => new(value);

        /// <summary>
        /// Convierte una falla en un resultado con falla.
        /// </summary>
        /// <param name="error">Falla del resultado.</param>
        public static implicit operator Result<T>(Error error) => new(error);

        /// <summary>
        /// Ejecuta la rama correspondiente al estado del resultado.
        /// </summary>
        /// <typeparam name="TOut">Tipo del valor devuelto.</typeparam>
        /// <param name="onSuccess">Función para el resultado con éxito.</param>
        /// <param name="onFailure">Función para el resultado con falla.</param>
        /// <returns>El valor devuelto por la rama ejecutada.</returns>
        public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
            => this.IsSuccess ? onSuccess(this.value) : onFailure(this.Error!);
    }
}
