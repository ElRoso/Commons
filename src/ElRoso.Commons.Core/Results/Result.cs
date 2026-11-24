// <copyright file="Result.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Results
{
    /// <summary>
    /// Representa el resultado de una operación con éxito o con falla.
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Result"/> class.
        /// ES: Inicializa una instancia con su estado y falla eventual.
        /// </summary>
        /// <param name="isSuccess">Indica si la operación terminó con éxito.</param>
        /// <param name="error">Falla asociada, nula cuando hay éxito.</param>
        protected Result(bool isSuccess, Error? error)
        {
            this.IsSuccess = isSuccess;
            this.Error = error;
        }

        /// <summary>
        /// Gets a value indicating whether the operation succeeded.
        /// ES: Indica si la operación terminó con éxito.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets a value indicating whether the operation failed.
        /// ES: Indica si la operación terminó con falla.
        /// </summary>
        public bool IsFailure => !this.IsSuccess;

        /// <summary>
        /// Gets the failure associated with the result.
        /// ES: Falla asociada, nula cuando hay éxito.
        /// </summary>
        public Error? Error { get; }

        /// <summary>
        /// Crea un resultado con éxito.
        /// </summary>
        /// <returns>El resultado con éxito.</returns>
        public static Result Success() => new(true, null);

        /// <summary>
        /// Crea un resultado con falla.
        /// </summary>
        /// <param name="error">Falla del resultado.</param>
        /// <returns>El resultado con falla.</returns>
        public static Result Failure(Error error) => new(false, error);

        /// <summary>
        /// Convierte una falla en un resultado con falla.
        /// </summary>
        /// <param name="error">Falla del resultado.</param>
        public static implicit operator Result(Error error) => Failure(error);
    }
}
