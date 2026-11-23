// <copyright file="TelegramBotRegistration.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    /// <summary>Internal DI marker used to pass bot name+token registrations to the factory.</summary>
    internal sealed class TelegramBotRegistration
    {
        public string Name { get; init; } = string.Empty;

        public string Token { get; init; } = string.Empty;
    }
}
