// <copyright file="BadRequestException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Exceptions
{
    using FluentValidation.Results;
    using System;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverageAttribute]
    [Serializable]

    public class BadRequestException : Exception
    {
        public IDictionary<string, string[]> Details { get; set; } = new Dictionary<string, string[]>();

        public BadRequestException(string? message)
            : base(message)
        {
        }

        public BadRequestException()
            : base("The server cannot or will not process the request due to something that is perceived to be a client error (e.g., malformed request syntax, invalid request message framing, or deceptive request routing).")
        {
        }

        public BadRequestException(ValidationResult validationResult)
        : base("The server cannot or will not process the request due to something that is perceived to be a client error (e.g., malformed request syntax, invalid request message framing, or deceptive request routing).")
        {
            this.Details = validationResult.Errors
                 .GroupBy(x => x.PropertyName)
                 .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray());
        }
    }
}
