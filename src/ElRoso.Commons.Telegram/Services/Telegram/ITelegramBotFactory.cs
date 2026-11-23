// <copyright file="ITelegramBotFactory.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    public interface ITelegramBotFactory
    {
        /// <summary>
        /// Returns the bot registered under <paramref name="botName"/>.
        /// Throws <see cref="InvalidOperationException"/> if no bot with that name was registered.
        /// Retorna el bot registrado bajo ese nombre. Lanza excepción si no existe.
        /// </summary>
        /// <returns></returns>
        ITelegramBot Get(string botName);
    }
}
