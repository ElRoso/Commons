// <copyright file="NoContentException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Exceptions
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverageAttribute]
    [Serializable]

    public class NoContentException : Exception
    {
        public NoContentException(string? message)
            : base(message)
        {
        }

        public NoContentException()
            : base("There is no content to send for this request, but the headers are useful. The user agent may update its cached headers for this resource with the new ones.")
        {
        }
    }
}
