// <copyright file="EnumResponse.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Domains.Responses
{
    using System.Globalization;
    using ElRoso.Helpers;

    public class EnumResponse : BaseResponse
    {
        public EnumResponse()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumResponse"/> class.
        /// EN: <paramref name="enum"/> must fit in a byte — the team declares its enums as
        ///     <c>: byte</c>, which is one byte in SQL Server (TINYINT) instead of four, in columns,
        ///     indexes and JWT payloads. An enum that does not follow that convention is rejected
        ///     here with an explanation, rather than through an OverflowException about a conversion
        ///     that says nothing to whoever wrote the enum.
        /// ES: <paramref name="enum"/> tiene que entrar en un byte — el equipo declara sus enums como
        ///     <c>: byte</c>, que es un byte en SQL Server (TINYINT) en vez de cuatro, en columnas,
        ///     índices y payloads JWT. Un enum que no sigue esa convención se rechaza acá con una
        ///     explicación, y no con un OverflowException sobre una conversión que no le dice nada a
        ///     quien escribió el enum.
        /// </summary>
        /// <param name="enum">The enum value to pack. / El valor de enum a empaquetar.</param>
        public EnumResponse(Enum @enum)
        {
            var numericValue = Convert.ToInt64(@enum, CultureInfo.InvariantCulture);

            if (numericValue is < byte.MinValue or > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(@enum),
                    numericValue,
                    $"'{@enum.GetType().Name}.{@enum}' does not fit in a byte. Domain enums are declared ': byte' so they take one byte instead of four in the database, its indexes and the token.");
            }

            this.DisplayName = @enum.GetDisplayName();
            this.Name = @enum.ToString();
            this.Value = (byte)numericValue;
        }

        public string? DisplayName { get; }

        public string Name { get; } = string.Empty;

        /// <summary>Gets the numeric value, one byte wide as the enum convention requires. / Valor numérico, de un byte como pide la convención de enums.</summary>
        public byte Value { get; }
    }
}
