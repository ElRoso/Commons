// <copyright file="AuditEntity.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Domains.Entities.NoSQL
{
    /// <summary>
    /// EN: Who wrote a document and when.
    ///     🔴 <see cref="CreatedBy"/> and <see cref="ModifiedBy"/> were <c>ObjectId?</c> until
    ///     2026-08-05, which is why they were never filled: the identity of the caller is a
    ///     <see cref="Guid"/> (<c>CurrentUser.Id</c>, from the token) and there is no meaningful
    ///     conversion between the two. Changing the type is safe precisely because of that bug —
    ///     the repository never wrote these fields, so every existing document has them null or
    ///     absent, and there is no stored value to migrate.
    ///     ⚠️ If a consuming app was filling them by hand with an ObjectId, that code has to change.
    /// ES: Quién escribió un documento y cuándo.
    ///     🔴 <see cref="CreatedBy"/> y <see cref="ModifiedBy"/> eran <c>ObjectId?</c> hasta el
    ///     2026-08-05, y por eso nunca se completaban: la identidad de quien llama es un
    ///     <see cref="Guid"/> (<c>CurrentUser.Id</c>, del token) y no hay conversión con sentido
    ///     entre los dos. Cambiar el tipo es seguro justamente por ese bug — el repositorio nunca
    ///     escribió estos campos, así que todo documento existente los tiene en null o ausentes, y
    ///     no hay ningún valor guardado que migrar.
    ///     ⚠️ Si alguna app consumidora los llenaba a mano con un ObjectId, ese código hay que
    ///     cambiarlo.
    /// </summary>
    public class AuditEntity
    {
        public DateTime CreationDate { get; set; }

        public string CreationDateToString
        {
            get { return this.CreationDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"); }
        }

        /// <summary>Gets or sets the id of the user who created the document. / Id de quien lo creó.</summary>
        public Guid? CreatedBy { get; set; }

        public DateTime ModificationDate { get; set; }

        public string ModificationDateToString
        {
            get { return this.ModificationDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"); }
        }

        /// <summary>Gets or sets the id of the user who last modified it. / Id de quien lo modificó.</summary>
        public Guid? ModifiedBy { get; set; }
    }
}
