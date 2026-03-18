using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("inventory_logs")]
public partial class InventoryLog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_supply_inventory_id")]
    public int? DepotSupplyInventoryId { get; set; }

    /// <summary>
    /// Set for Reusable item log entries — one log row per individual unit.
    /// Null for Consumable log entries (which use DepotSupplyInventoryId instead).
    /// </summary>
    [Column("reusable_item_id")]
    public int? ReusableItemId { get; set; }

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
    public virtual SupplyInventory? SupplyInventory { get; set; }

    [ForeignKey("ReusableItemId")]
    public virtual ReusableItem? ReusableItem { get; set; }

    [ForeignKey("PerformedBy")]
    [InverseProperty("InventoryLogs")]
    public virtual User? PerformedByUser { get; set; }

    [ForeignKey("VatInvoiceId")]
    [InverseProperty("InventoryLogs")]
    public virtual VatInvoice? VatInvoice { get; set; }
}
