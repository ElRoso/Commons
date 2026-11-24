// <copyright file="ResultExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.AspNetCore.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ElRoso.Results;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.WebUtilities;

    /// <summary>
    /// Convierte resultados de dominio en respuestas HTTP de Minimal APIs y controllers.
    /// </summary>
    public static class ResultExtensions
    {
        /// <summary>
        /// Convierte un resultado sin valor en una respuesta HTTP.
        /// </summary>
        /// <param name="result">Resultado a convertir.</param>
        /// <returns>204 con éxito, o Problem Details con la falla.</returns>
        public static IResult ToHttpResult(this Result result)
        {
            ArgumentNullException.ThrowIfNull(result);

            return result.IsSuccess
                ? TypedResults.NoContent()
                : ToProblemResult(result.Error!);
        }

        /// <summary>
        /// Convierte un resultado con valor en una respuesta HTTP.
        /// </summary>
        /// <typeparam name="T">Tipo del valor.</typeparam>
        /// <param name="result">Resultado a convertir.</param>
        /// <param name="onSuccess">Función opcional para armar la respuesta con éxito.</param>
        /// <returns>200 con el valor por defecto, o Problem Details con la falla.</returns>
        public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (result.IsFailure)
            {
                return ToProblemResult(result.Error!);
            }

            return onSuccess is null ? TypedResults.Ok(result.Value) : onSuccess(result.Value);
        }

        /// <summary>
        /// Convierte un resultado sin valor en una respuesta de controller.
        /// </summary>
        /// <param name="result">Resultado a convertir.</param>
        /// <param name="controller">Controller que crea la respuesta.</param>
        /// <returns>204 con éxito, o Problem Details con la falla.</returns>
        public static IActionResult ToActionResult(this Result result, ControllerBase controller)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(controller);

            return result.IsSuccess
                ? controller.NoContent()
                : ToProblemActionResult(controller, result.Error!);
        }

        /// <summary>
        /// Convierte un resultado con valor en una respuesta de controller.
        /// </summary>
        /// <typeparam name="T">Tipo del valor.</typeparam>
        /// <param name="result">Resultado a convertir.</param>
        /// <param name="controller">Controller que crea la respuesta.</param>
        /// <param name="onSuccess">Función opcional para armar la respuesta con éxito.</param>
        /// <returns>200 con el valor por defecto, o Problem Details con la falla.</returns>
        public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller, Func<T, IActionResult>? onSuccess = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(controller);

            if (result.IsFailure)
            {
                return ToProblemActionResult(controller, result.Error!);
            }

            return onSuccess is null ? OkAsJson(controller, result.Value) : onSuccess(result.Value);
        }

        private static IResult ToProblemResult(Error error)
        {
            var status = MapStatus(error.Type);
            var extensions = CreateExtensions(error);

            return error.Type == ErrorType.Validation
                ? TypedResults.ValidationProblem(
                    error.Details.ToDictionary(detail => detail.Key, detail => detail.Value),
                    detail: error.Message,
                    title: ReasonPhrases.GetReasonPhrase(status),
                    extensions: extensions)
                : TypedResults.Problem(
                    statusCode: status,
                    title: ReasonPhrases.GetReasonPhrase(status),
                    detail: error.Message,
                    extensions: extensions);
        }

        private static IActionResult ToProblemActionResult(ControllerBase controller, Error error)
        {
            var status = MapStatus(error.Type);
            var extensions = CreateExtensions(error);

            if (error.Type != ErrorType.Validation)
            {
                return controller.Problem(
                    detail: error.Message,
                    statusCode: status,
                    title: ReasonPhrases.GetReasonPhrase(status),
                    extensions: extensions);
            }

            var modelState = new ModelStateDictionary();
            foreach (var detail in error.Details)
            {
                foreach (var message in detail.Value)
                {
                    modelState.AddModelError(detail.Key, message);
                }
            }

            return controller.ValidationProblem(
                detail: error.Message,
                statusCode: status,
                title: ReasonPhrases.GetReasonPhrase(status),
                modelStateDictionary: modelState,
                extensions: extensions);
        }

        private static IActionResult OkAsJson(ControllerBase controller, object? value)
        {
            var result = controller.Ok(value);
            if (result is OkObjectResult objectResult)
            {
                // El formateador de strings de MVC escribiría texto plano en lugar de JSON.
                objectResult.ContentTypes.Clear();
                objectResult.ContentTypes.Add("application/json");
            }

            return result;
        }

        private static Dictionary<string, object?> CreateExtensions(Error error) =>
            new() { ["code"] = error.Code };

        private static int MapStatus(ErrorType type) => type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}
