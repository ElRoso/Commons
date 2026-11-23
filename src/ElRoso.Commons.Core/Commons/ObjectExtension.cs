// <copyright file="ObjectExtension.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Commons
{
    using Microsoft.AspNetCore.Http;

    public static class ObjectExtension
    {
        public static bool HasValue<T>(this T obj)
        {
            if (obj == null)
            {
                return false;
            }

            var type = typeof(T);

            if (type == typeof(int))
            {
                return (int)(object)obj != 0;
            }
            else if (type == typeof(string))
            {
                return !string.IsNullOrEmpty((string)(object)obj);
            }

            return true;
        }

        /// <summary>
        /// EN: A list has a value when at least one of its items does.
        ///     The rule used to change with the length: with exactly one item the element was
        ///     evaluated, with two or more the contents were never looked at — so a list of two empty
        ///     strings claimed to have a value while a list of one empty string did not. Same
        ///     question, opposite answers, decided by the count.
        /// ES: Una lista tiene valor cuando al menos uno de sus elementos lo tiene.
        ///     La regla cambiaba con el largo: con exactamente un elemento se evaluaba el elemento,
        ///     con dos o más no se miraba el contenido — así que una lista de dos cadenas vacías
        ///     decía tener valor y una de una sola cadena vacía decía que no. La misma pregunta,
        ///     respuestas opuestas, decididas por la cantidad.
        /// </summary>
        /// <returns>Whether any item has a value. / Si algún elemento tiene valor.</returns>
        public static bool HasValue<T>(this List<T> obj)
        {
            if (obj == null || obj.Count == 0)
            {
                return false;
            }

            return obj.Any(item => item.HasValue());
        }

        public static async Task<byte[]> ToByteArrayAsync(this IFormFile formFile)
        {
            await using var memoryStream = new MemoryStream();
            await formFile.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
