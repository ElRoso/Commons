// <copyright file="FileNotFoundException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Exceptions
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// EN: A file or object the caller asked for does not exist. Derives from
    ///     <see cref="NotFoundException"/>, so it answers <b>404</b> with its message intact.
    ///     🔴 Storage services must throw THIS one, not <see cref="System.IO.FileNotFoundException"/>:
    ///     that one is not a domain exception, so it came back as a 500 with the message masked in
    ///     production. Keep them apart — a System.IO failure reading the local disk really is an
    ///     internal error.
    /// ES: Un archivo u objeto que pidió quien llamó no existe. Hereda de
    ///     <see cref="NotFoundException"/>, así que responde <b>404</b> con su mensaje intacto.
    ///     🔴 Los servicios de almacenamiento tienen que lanzar ÉSTA, no la de
    ///     <see cref="System.IO.FileNotFoundException"/>: esa no es excepción de dominio, así que
    ///     volvía como 500 con el mensaje enmascarado en producción. Mantenerlas separadas — una
    ///     falla de System.IO leyendo el disco local sí es un error interno.
    /// </summary>
    [ExcludeFromCodeCoverageAttribute]
    [Serializable]
    public class FileNotFoundException : NotFoundException
    {
        public FileNotFoundException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileNotFoundException"/> class, keeping the
        /// storage provider's original failure as the inner exception.
        /// Conserva la falla original del proveedor de almacenamiento como excepción interna.
        /// </summary>
        /// <param name="message">User-facing message. / Mensaje para quien llamó.</param>
        /// <param name="innerException">The provider exception. / La excepción del proveedor.</param>
        public FileNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public FileNotFoundException()
            : base("File not found")
        {
        }
    }
}
