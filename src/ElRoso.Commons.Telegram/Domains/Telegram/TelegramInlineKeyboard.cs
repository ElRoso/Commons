// <copyright file="TelegramInlineKeyboard.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Telegram
{
    /// <summary>
    /// EN: The buttons shown under a message, laid out in rows. A button either opens a URL or sends
    ///     a callback back to the bot — Telegram rejects one that does neither, and it rejects one
    ///     that does both.
    /// ES: Los botones que se muestran debajo de un mensaje, ordenados en filas. Un botón abre una
    ///     URL o manda un callback de vuelta al bot — Telegram rechaza el que no hace ninguna de las
    ///     dos, y rechaza el que hace las dos.
    /// </summary>
    public sealed class TelegramInlineKeyboard
    {
        /// <summary>Gets the rows of buttons. / Las filas de botones.</summary>
        public IList<IList<TelegramInlineButton>> Rows { get; } = [];

        /// <summary>
        /// Adds a row of buttons. / Agrega una fila de botones.
        /// </summary>
        /// <param name="buttons">The buttons of the row. / Los botones de la fila.</param>
        /// <returns>The same keyboard, to chain calls. / El mismo teclado, para encadenar llamadas.</returns>
        public TelegramInlineKeyboard AddRow(params TelegramInlineButton[] buttons)
        {
            this.Rows.Add([.. buttons]);

            return this;
        }
    }
}
