// <copyright file="ForbiddenException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Exceptions
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverageAttribute]
    [Serializable]

    public class ForbiddenException : Exception
    {
        public ForbiddenException(string? message)
            : base(message)
        {
        }

        public ForbiddenException()
            : base("The client does not have access rights to the content; that is, it is unauthorized, so the server is refusing to give the requested resource. Unlike 401 Unauthorized, the client's identity is known to the server.")
        {
        }
    }
}
