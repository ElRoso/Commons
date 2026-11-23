// <copyright file="HealthCheckResponse.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Domains.Responses
{
    /// <summary>
    /// Health check response with overall status and individual check results.
    /// Respuesta de health check con estado general y resultados individuales.
    /// </summary>
    public class HealthCheckResponse : BaseResponse
    {
        public string Status { get; set; } = string.Empty;

        public IEnumerable<IndividualHealthCheckResponse> HealthChecks { get; set; } = [];

        public TimeSpan HealthCheckDuration { get; set; }
    }
}
