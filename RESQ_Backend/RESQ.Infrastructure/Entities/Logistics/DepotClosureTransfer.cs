using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("depot_closure_transfers")]
public class DepotClosureTransfer
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("closure_id")]
    public int ClosureId { get; set; }

    [Column("source_depot_id")]
    public int SourceDepotId { get; set; }

    [Column("target_depot_id")]
    public int TargetDepotId { get; set; }

    [Column("status")]
    [StringLength(30)]
    public string Status { get; set; } = "AwaitingShipment";

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("transfer_deadline_at", TypeName = "timestamp with time zone")]
    public DateTime TransferDeadlineAt { get; set; }

    // Source side
    [Column("shipped_at", TypeName = "timestamp with time zone")]
    public DateTime? ShippedAt { get; set; }

    [Column("shipped_by")]
    public Guid? ShippedBy { get; set; }

    [Column("ship_note")]
    public string? ShipNote { get; set; }

    // Target side
    [Column("received_at", TypeName = "timestamp with time zone")]
    public DateTime? ReceivedAt { get; set; }

    [Column("received_by")]
    public Guid? ReceivedBy { get; set; }

    [Column("receive_note")]
    public string? ReceiveNote { get; set; }

    // Snapshot at time of creation
    [Column("snapshot_consumable_units")]
    public int SnapshotConsumableUnits { get; set; }

    [Column("snapshot_reusable_units")]
    public int SnapshotReusableUnits { get; set; }

    // Cancel
    [Column("cancelled_at", TypeName = "timestamp with time zone")]
    public DateTime? CancelledAt { get; set; }

    [Column("cancelled_by")]
    public Guid? CancelledBy { get; set; }

    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

    // Navigation property
    [ForeignKey(nameof(ClosureId))]
    public DepotClosure? Closure { get; set; }
}
