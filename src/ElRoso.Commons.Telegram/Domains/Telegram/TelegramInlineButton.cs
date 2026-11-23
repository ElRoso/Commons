// <copyright file="TelegramInlineButton.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Telegram
{
    /// <summary>
    /// EN: One button. Build it with <see cref="Callback"/> or <see cref="Link"/> — the constructor
    ///     is not public because a button with both a URL and a callback, or with neither, is
    ///     rejected by Telegram, and that is a runtime 400 for something the compiler can prevent.
    /// ES: Un botón. Se arma con <see cref="Callback"/> o <see cref="Link"/> — el constructor no es
    ///     público porque un botón con URL y callback a la vez, o sin ninguno de los dos, lo rechaza
    ///     Telegram, y eso es un 400 en runtime por algo que el compilador puede evitar.
    /// </summary>
    public sealed class TelegramInlineButton
    {
        private TelegramInlineButton(string text, string? callbackData, string? url)
        {
            this.Text = text;
            this.CallbackData = callbackData;
            this.Url = url;
        }

        /// <summary>Gets the label. / La etiqueta.</summary>
        public string Text { get; }

        /// <summary>Gets the data sent back to the bot when tapped. / El dato que vuelve al bot al tocarlo.</summary>
        public string? CallbackData { get; }

        /// <summary>Gets the URL the button opens. / La URL que abre el botón.</summary>
        public string? Url { get; }

        /// <summary>
        /// A button that sends <paramref name="callbackData"/> back to the bot.
        /// ES: Un botón que manda <paramref name="callbackData"/> de vuelta al bot.
        /// </summary>
        /// <param name="text">The label. / La etiqueta.</param>
        /// <param name="callbackData">The data, at most 64 bytes as Telegram requires. / El dato, 64 bytes como máximo según Telegram.</param>
        /// <returns>The button. / El botón.</returns>
        public static TelegramInlineButton Callback(string text, string callbackData)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            ArgumentException.ThrowIfNullOrWhiteSpace(callbackData);

            // EN: Telegram caps callback data at 64 bytes and answers 400 when it is longer. Failing
            //     here names the button; failing there names nothing.
            // ES: Telegram limita el callback a 64 bytes y responde 400 cuando es más largo. Fallar
            //     acá nombra el botón; fallar allá no nombra nada.
            if (System.Text.Encoding.UTF8.GetByteCount(callbackData) > 64)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(callbackData),
                    $"The callback data of button '{text}' is longer than the 64 bytes Telegram accepts.");
            }

            return new TelegramInlineButton(text, callbackData, url: null);
        }

        /// <summary>
        /// A button that opens <paramref name="url"/>. / Un botón que abre <paramref name="url"/>.
        /// </summary>
        /// <param name="text">The label. / La etiqueta.</param>
        /// <param name="url">The absolute URL. / La URL absoluta.</param>
        /// <returns>The button. / El botón.</returns>
        public static TelegramInlineButton Link(string text, string url)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            return new TelegramInlineButton(text, callbackData: null, url);
        }
    }
}
