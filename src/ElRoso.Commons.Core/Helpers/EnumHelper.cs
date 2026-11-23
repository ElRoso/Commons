// <copyright file="EnumHelper.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Helpers
{
    using System.ComponentModel.DataAnnotations;
    using System.Reflection;

    /// <summary>Extension methods for <see cref="Enum"/> values.</summary>
    public static class EnumHelper
    {
        /// <summary>Returns the <see cref="DisplayAttribute.Name"/> of the enum member, or <c>null</c> if the attribute is not present.</summary>
        /// <param name="enumValue">The enum value to inspect.</param>
        /// <returns>The display name, or <c>null</c>.</returns>
        public static string? GetDisplayName(this Enum enumValue) => enumValue.GetAttribute<DisplayAttribute>()?.Name;

        /// <summary>
        ///     A generic extension method that aids in reflecting
        ///     and retrieving any attribute that is applied to an `Enum`.
        /// </summary>
        private static TAttribute? GetAttribute<TAttribute>(this Enum enumValue)
            where TAttribute : Attribute
        {
            return enumValue.GetType()
                .GetMember(enumValue.ToString())
                .First()
                .GetCustomAttribute<TAttribute>();
        }
    }
}
