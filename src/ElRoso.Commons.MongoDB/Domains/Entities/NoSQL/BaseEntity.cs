// <copyright file="BaseEntity.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Domains.Entities.NoSQL
{
    using MongoDB.Bson;

    public class BaseEntity
    {
        public ObjectId? Id { get; set; }

        public AuditEntity Audit { get; set; } = new AuditEntity();
    }
}
