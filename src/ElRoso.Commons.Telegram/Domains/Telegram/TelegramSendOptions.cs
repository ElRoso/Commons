// <copyright file="TelegramSendOptions.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Telegram
{
    /// <summary>
    /// EN: How a message goes out: formatting, whether it makes the phone buzz, and the buttons
    ///     under it. Everything optional — with no options the message goes out exactly as it did
    ///     before this type existed, as plain text.
    ///     It is one object instead of four parameters because Telegram keeps adding fields to
    ///     sendMessage, and each one added as a parameter is a change to the interface.
    /// ES: Cómo sale un mensaje: formato, si hace sonar el teléfono, y los botones de abajo. Todo
    ///     opcional — sin opciones el mensaje sale exactamente igual que antes de que este tipo
    ///     existiera, como texto plano.
    ///     Es un objeto y no cuatro parámetros porque Telegram le sigue agregando campos a
    ///     sendMessage, y cada uno agregado como parámetro es un cambio de la interfaz.
    /// </summary>
    public sealed class TelegramSendOptions
    {
        /// <summary>Gets or sets how Telegram should parse the text. / Cómo debe interpretar el texto Telegram.</summary>
        public TelegramParseMode ParseMode { get; set; } = TelegramParseMode.None;

        /// <summary>
        /// Gets or sets a value indicating whether the message arrives without a notification.
        /// ES: Si el mensaje llega sin notificación.
        /// </summary>
        public bool DisableNotification { get; set; }

        /// <summary>Gets or sets the buttons shown under the message. / Los botones que se muestran debajo del mensaje.</summary>
        public TelegramInlineKeyboard? InlineKeyboard { get; set; }
    }
}
