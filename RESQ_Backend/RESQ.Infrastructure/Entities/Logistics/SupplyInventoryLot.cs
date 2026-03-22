using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("supply_inventory_lots")]
public partial class SupplyInventoryLot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("supply_inventory_id")]
    public int SupplyInventoryId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("remaining_quantity")]
    public int RemainingQuantity { get; set; }

    [Column("received_date", TypeName = "timestamp with time zone")]
    public DateTime? ReceivedDate { get; set; }

    [Column("expired_date", TypeName = "timestamp with time zone")]
    public DateTime? ExpiredDate { get; set; }

    [Column("source_type")]
    [StringLength(50)]
    public string? SourceType { get; set; }

    [Column("source_id")]
    public int? SourceId { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("SupplyInventoryId")]
    [InverseProperty("Lots")]
    public virtual SupplyInventory SupplyInventory { get; set; } = null!;

    [InverseProperty("SupplyInventoryLot")]
    public virtual ICollection<InventoryLog> InventoryLogs { get; set; } = new List<InventoryLog>();
}
