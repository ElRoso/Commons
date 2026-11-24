// <copyright file="Error.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Outcomes
{
    using FluentValidation.Results;

    /// <summary>
    /// Describe una falla con código, mensaje, tipo y detalles por propiedad.
    /// La igualdad del record compara código, mensaje y tipo; Details se compara por referencia.
    /// </summary>
    public sealed record Error
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Error"/> class.
        /// ES: Inicializa una falla con su código, mensaje y tipo.
        /// </summary>
        /// <param name="code">Código estable de la falla.</param>
        /// <param name="message">Mensaje para quien llamó.</param>
        /// <param name="type">Clasificación de la falla.</param>
        public Error(string code, string message, ErrorType type)
        {
            this.Code = code;
            this.Message = message;
            this.Type = type;
        }

        /// <summary>
        /// Gets the stable failure code.
        /// ES: Código estable de la falla.
        /// </summary>
        public string Code { get; init; }

        /// <summary>
        /// Gets the message intended for the caller.
        /// ES: Mensaje para quien llamó.
        /// </summary>
        public string Message { get; init; }

        /// <summary>
        /// Gets the failure classification.
        /// ES: Clasificación de la falla.
        /// </summary>
        public ErrorType Type { get; init; }

        /// <summary>
        /// Gets the failure details grouped by property.
        /// ES: Detalles de la falla agrupados por propiedad.
        /// </summary>
        public IReadOnlyDictionary<string, string[]> Details { get; init; } = new Dictionary<string, string[]>();

        /// <summary>
        /// Crea una falla de validación con sus detalles.
        /// </summary>
        /// <param name="code">Código de la falla.</param>
        /// <param name="message">Mensaje de la falla.</param>
        /// <param name="details">Detalles agrupados por propiedad.</param>
        /// <returns>La falla de validación.</returns>
        public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]> details)
            => new(code, message, ErrorType.Validation) { Details = details };

        /// <summary>
        /// Crea una falla de recurso no encontrado.
        /// </summary>
        /// <param name="code">Código de la falla.</param>
        /// <param name="message">Mensaje de la falla.</param>
        /// <returns>La falla de recurso no encontrado.</returns>
        public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

        /// <summary>
        /// Crea una falla de conflicto.
        /// </summary>
        /// <param name="code">Código de la falla.</param>
        /// <param name="message">Mensaje de la falla.</param>
        /// <returns>La falla de conflicto.</returns>
        public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

        /// <summary>
        /// Crea una falla de autenticación.
        /// </summary>
        /// <param name="code">Código de la falla.</param>
        /// <param name="message">Mensaje de la falla.</param>
        /// <returns>La falla de autenticación.</returns>
        public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

        /// <summary>
        /// Crea una falla de autorización.
        /// </summary>
        /// <param name="code">Código de la falla.</param>
        /// <param name="message">Mensaje de la falla.</param>
        /// <returns>La falla de autorización.</returns>
        public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

        /// <summary>
        /// Crea una falla genérica.
        /// </summary>
        /// <param name="code">Código de la falla.</param>
        /// <param name="message">Mensaje de la falla.</param>
        /// <returns>La falla genérica.</returns>
        public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

        /// <summary>
        /// Convierte un resultado de validación en una falla agrupada por propiedad.
        /// </summary>
        /// <param name="validationResult">Resultado de validación de origen.</param>
        /// <returns>La falla de validación con sus detalles.</returns>
        public static Error FromValidation(ValidationResult validationResult)
        {
            var details = validationResult.Errors
                .GroupBy(failure => failure.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray());

            return Validation("validation", "One or more validation errors occurred.", details);
        }
    }
}
