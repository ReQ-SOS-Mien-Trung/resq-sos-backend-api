using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("item_categories")]
public partial class ItemCategory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    [StringLength(50)]
    public string? Code { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string? Name { get; set; }

    [Column("quantity")]
    public int? Quantity { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    // FIXED: Changed "Category" to "ItemCategory" to match the property in ReliefItem.cs
    [InverseProperty("ItemCategory")]
    public virtual ICollection<ReliefItem> ReliefItems { get; set; } = new List<ReliefItem>();
}
