// <copyright file="ElRosoServiceBase.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using Microsoft.Extensions.Logging;

    public class ElRosoServiceBase : IElRosoServiceBase
    {
        private readonly ILogger logger;

        public ElRosoServiceBase(ILogger logger)
        {
            this.logger = logger;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
