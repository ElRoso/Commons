// <copyright file="TelegramParseMode.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Telegram
{
    /// <summary>
    /// EN: How Telegram should read the text of a message. The default is <see cref="None"/> — plain
    ///     text, which is also the only thing this library could send until now.
    ///     ⚠️ Picking a mode makes Telegram reject any text whose markup is malformed: an unmatched
    ///     '*' or a bare '_' in a name is a 400, not a message that arrives ugly.
    /// ES: Cómo debe leer Telegram el texto de un mensaje. El default es <see cref="None"/> — texto
    ///     plano, que además es lo único que esta librería podía mandar hasta ahora.
    ///     ⚠️ Elegir un modo hace que Telegram rechace cualquier texto con markup mal formado: un '*'
    ///     sin cerrar o un '_' suelto en un nombre es un 400, no un mensaje que llega feo.
    /// </summary>
    public enum TelegramParseMode : byte
    {
        /// <summary>Plain text. / Texto plano.</summary>
        None = 0,

        /// <summary>Legacy Markdown. / Markdown viejo.</summary>
        Markdown = 1,

        /// <summary>MarkdownV2 — the current one. / MarkdownV2, el actual.</summary>
        MarkdownV2 = 2,

        /// <summary>A subset of HTML. / Un subconjunto de HTML.</summary>
        Html = 3,
    }
}
