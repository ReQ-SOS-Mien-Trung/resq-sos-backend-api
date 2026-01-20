using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Domain.Entities;

[Table("organization_relief_items")]
public partial class OrganizationReliefItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("organization_id")]
    public int? OrganizationId { get; set; }

    [Column("relief_item_id")]
    public int? ReliefItemId { get; set; }

    [Column("received_date")]
    public DateOnly? ReceivedDate { get; set; }

    [Column("expired_date")]
    public DateOnly? ExpiredDate { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [ForeignKey("OrganizationId")]
    [InverseProperty("OrganizationReliefItems")]
    public virtual Organization? Organization { get; set; }

    [ForeignKey("ReliefItemId")]
    [InverseProperty("OrganizationReliefItems")]
    public virtual ReliefItem? ReliefItem { get; set; }
}
