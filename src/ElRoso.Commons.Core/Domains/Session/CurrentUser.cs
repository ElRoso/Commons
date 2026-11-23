// <copyright file="CurrentUser.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Domains.Session
{
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverage]
    public class CurrentUser
    {
        public required Guid Id { get; set; }

        public required string Surname { get; set; }

        public required string GivenName { get; set; }

        public required string EmailAddress { get; set; }

        public string BusinessName => $"{this.Surname}, {this.GivenName}";
    }
}
