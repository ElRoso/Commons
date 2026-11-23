// <copyright file="BaseEntity.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Domains.Entities.SQL
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class BaseEntity
    {
        [Column(Order = 0)]
        [Key]
        [Required(ErrorMessage = "{0} is required")]
        public required Guid Id { get; set; } = Guid.NewGuid();

        [Column(Order = 96)]
        public required DateTime CreationDate { get; set; }

        [Column(Order = 97)]
        public Guid? CreatedBy { get; set; }

        [Column(Order = 98)]
        public required DateTime ModificationDate { get; set; }

        [Column(Order = 99)]
        public Guid? ModifiedBy { get; set; }
    }
}
