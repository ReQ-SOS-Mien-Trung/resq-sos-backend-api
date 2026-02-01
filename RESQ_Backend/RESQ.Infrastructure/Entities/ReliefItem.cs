using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities;

namespace RESQ.Infrastructure.Entities;

[Table("relief_items")]
public partial class ReliefItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("category_id")]
    public int? CategoryId { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string? Name { get; set; }

    [Column("unit")]
    [StringLength(50)]
    public string? Unit { get; set; }

    [Column("target_group")]
    [StringLength(50)]
    public string? TargetGroup { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp without time zone")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("ReliefItems")]
    public virtual ItemCategory? ItemCategory { get; set; }

    [InverseProperty("ReliefItem")]
    public virtual ICollection<DepotSupplyInventory> DepotSupplyInventories { get; set; } = new List<DepotSupplyInventory>();

    [InverseProperty("ReliefItem")]
    public virtual ICollection<MissionItem> MissionItems { get; set; } = new List<MissionItem>();

    [InverseProperty("ReliefItem")]
    public virtual ICollection<OrganizationReliefItem> OrganizationReliefItems { get; set; } = new List<OrganizationReliefItem>();
}
