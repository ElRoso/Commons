// <copyright file="NotFoundException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Exceptions
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverageAttribute]
    [Serializable]

    public class NotFoundException : Exception
    {
        public NotFoundException(string? message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NotFoundException"/> class, keeping the
        /// original failure as the inner exception.
        /// Conserva la falla original como excepción interna.
        /// </summary>
        /// <param name="message">User-facing message. / Mensaje para quien llamó.</param>
        /// <param name="innerException">The underlying failure. / La falla subyacente.</param>
        public NotFoundException(string? message, Exception innerException)
            : base(message, innerException)
        {
        }

        public NotFoundException()
            : base("The server cannot find the requested resource. In the browser, this means the URL is not recognized. In an API, this can also mean that the endpoint is valid but the resource itself does not exist. Servers may also send this response instead of 403 Forbidden to hide the existence of a resource from an unauthorized client. This response code is probably the most well known due to its frequent occurrence on the web.")
        {
        }
    }
}
