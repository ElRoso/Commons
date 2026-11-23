// <copyright file="EmailResponse.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Domains.Responses
{
    /// <summary>
    /// Response model for email send operations.
    /// Modelo de respuesta para operaciones de envío de email.
    /// </summary>
    public class EmailResponse : BaseResponse
    {
        public bool SendOK { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
