// <copyright file="TelegramBotFactory.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    internal sealed class TelegramBotFactory(IReadOnlyDictionary<string, ITelegramBot> bots) : ITelegramBotFactory
    {
        public ITelegramBot Get(string botName)
        {
            if (!bots.TryGetValue(botName, out var bot))
            {
                throw new InvalidOperationException($"Telegram bot '{botName}' is not registered. Call AddTelegramBot(\"{botName}\", ...) in your DI setup.");
            }

            return bot;
        }
    }
}
