// <copyright file="IndividualHealthCheckResponse.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Domains.Responses
{
    public class IndividualHealthCheckResponse : BaseResponse
    {
        public string Status { get; set; } = string.Empty;

        public string Component { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }
}
