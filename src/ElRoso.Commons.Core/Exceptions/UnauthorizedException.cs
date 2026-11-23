// <copyright file="UnauthorizedException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Exceptions
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverageAttribute]
    [Serializable]

    public class UnauthorizedException : Exception
    {
        public UnauthorizedException(string? message)
    : base(message)
        {
        }

        public UnauthorizedException()
            : base("Although the HTTP standard specifies \"unauthorized\", semantically this response means \"unauthenticated\". That is, the client must authenticate itself to get the requested response.")
        {
        }
    }
}
