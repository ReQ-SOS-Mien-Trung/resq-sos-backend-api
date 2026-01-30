using RESQ.Infrastructure.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities;

[Table("inventory_logs")]
public partial class InventoryLog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_inventory_id")]
    public int? DepotInventoryId { get; set; }

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

    [Column("performed_by")]
    public Guid? PerformedBy { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("DepotInventoryId")]
    [InverseProperty("InventoryLogs")]
    public virtual DepotInventory? DepotInventory { get; set; }

    [ForeignKey("PerformedBy")]
    [InverseProperty("InventoryLogs")]
    public virtual User? PerformedByNavigation { get; set; }
}
