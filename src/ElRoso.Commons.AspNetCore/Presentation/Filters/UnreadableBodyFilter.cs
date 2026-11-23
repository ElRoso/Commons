// <copyright file="UnreadableBodyFilter.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Presentation.Filters
{
    using ElRoso.Exceptions;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Mvc.ModelBinding;

    /// <summary>Lanza BadRequestException cuando el body no se pudo leer y el parámetro llegaría null a la acción.</summary>
    public sealed class UnreadableBodyFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var body = context.ActionDescriptor.Parameters.FirstOrDefault(p => p.BindingInfo?.BindingSource == BindingSource.Body);
            if (body is null || context.ModelState.IsValid || (context.ActionArguments.TryGetValue(body.Name, out var value) && value is not null))
            {
                return;
            }

            throw new BadRequestException("El cuerpo del pedido no es válido.")
            {
                Details = context.ModelState
                    .Where(entry => entry.Value!.Errors.Count > 0)
                    .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors.Select(e => e.ErrorMessage).ToArray()),
            };
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
