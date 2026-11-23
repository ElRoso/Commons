// <copyright file="GenericResponse.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Domains.Responses
{
    public class GenericResponse<T> : BaseResponse
    {
        public T Data { get; set; } = default!;

        public int Count { get; set; }
    }
}
