using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("organization_relief_items")]
public partial class OrganizationReliefItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("organization_id")]
    public int? OrganizationId { get; set; }

    [Column("item_model_id")]
    public int? ItemModelId { get; set; }

    [Column("quantity")]
    public int? Quantity { get; set; }

    [Column("received_date")]
    public DateOnly? ReceivedDate { get; set; }

    [Column("expired_date")]
    public DateOnly? ExpiredDate { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("received_by")]
    public Guid? ReceivedBy { get; set; }

    [Column("received_at")]
    public int? ReceivedAt { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("OrganizationId")]
    [InverseProperty("OrganizationReliefItems")]
    public virtual Organization? Organization { get; set; }

    [ForeignKey("ItemModelId")]
    [InverseProperty("OrganizationReliefItems")]
    public virtual ItemModel? ItemModel { get; set; }
}
