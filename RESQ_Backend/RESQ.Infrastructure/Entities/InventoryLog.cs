using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("inventory_logs")]
public partial class InventoryLog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_supply_inventory_id")]
    public int? DepotSupplyInventoryId { get; set; }

    [Column("vat_invoice_id")]
    public int? VatInvoiceId { get; set; }

    [Column("action_type")]
    [StringLength(50)]
    public string? ActionType { get; set; }

    [Column("quantity_change")]
    public int? QuantityChange { get; set; }

    [Column("source_type")]
    [StringLength(50)]
    public string? SourceType { get; set; }

    [Column("source_id")]
    public int? SourceId { get; set; }

    [Column("mission_id")]
    public int? MissionId { get; set; }

    [Column("performed_by")]
    public Guid? PerformedBy { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("DepotSupplyInventoryId")]
    [InverseProperty("InventoryLogs")]
    public virtual DepotSupplyInventory? DepotSupplyInventory { get; set; }

    [ForeignKey("PerformedBy")]
    [InverseProperty("InventoryLogs")]
    public virtual User? PerformedByUser { get; set; }

    [ForeignKey("VatInvoiceId")]
    [InverseProperty("InventoryLogs")]
    public virtual VatInvoice? VatInvoice { get; set; }
}
