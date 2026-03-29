using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("item_models")]
public partial class ItemModel
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("category_id")]
    public int? CategoryId { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string? Name { get; set; }

    [Column("description")]
    [StringLength(1000)]
    public string? Description { get; set; }

    [Column("unit")]
    [StringLength(50)]
    public string? Unit { get; set; }

    [Column("item_type")]
    [StringLength(50)]
    public string? ItemType { get; set; }

    [Column("image_url")]
    [StringLength(2048)]
    public string? ImageUrl { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("ItemModels")]
    public virtual Category? Category { get; set; }

    [InverseProperty("ItemModel")]
    public virtual ICollection<SupplyInventory> SupplyInventories { get; set; } = new List<SupplyInventory>();

    [InverseProperty("ItemModel")]
    public virtual ICollection<MissionItem> MissionItems { get; set; } = new List<MissionItem>();

    [InverseProperty("ItemModel")]
    public virtual ICollection<OrganizationReliefItem> OrganizationReliefItems { get; set; } = new List<OrganizationReliefItem>();

    [InverseProperty("ItemModel")]
    public virtual ICollection<VatInvoiceItem> VatInvoiceItems { get; set; } = new List<VatInvoiceItem>();

    [InverseProperty("ItemModel")]
    public virtual ICollection<DepotSupplyRequestItem> DepotSupplyRequestItems { get; set; } = new List<DepotSupplyRequestItem>();

    [InverseProperty("ItemModel")]
    public virtual ICollection<ReusableItem> ReusableItems { get; set; } = new List<ReusableItem>();

    public virtual ICollection<TargetGroup> TargetGroups { get; set; } = new List<TargetGroup>();
}
