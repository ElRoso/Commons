// <copyright file="ProblemResponse.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Domains.Responses
{
    using System.Net;

    public class ProblemResponse : BaseResponse
    {
        public HttpStatusCode Status { get; set; }

        public required string Title { get; set; }

        public IDictionary<string, string[]> Details { get; set; } = new Dictionary<string, string[]>();

        public string? TraceId { get; set; }
    }
}
