using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("depot_closures")]
public class DepotClosure
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_id")]
    public int DepotId { get; set; }

    [Column("initiated_by")]
    public Guid InitiatedBy { get; set; }

    [Column("initiated_at", TypeName = "timestamp with time zone")]
    public DateTime InitiatedAt { get; set; }

    [Column("previous_status")]
    [StringLength(50)]
    public string PreviousStatus { get; set; } = string.Empty;

    [Column("close_reason")]
    public string CloseReason { get; set; } = string.Empty;

    [Column("status")]
    [StringLength(30)]
    public string Status { get; set; } = "InProgress";

    [Column("snapshot_consumable_units")]
    public int SnapshotConsumableUnits { get; set; }

    [Column("snapshot_reusable_units")]
    public int SnapshotReusableUnits { get; set; }

    [Column("actual_consumable_units")]
    public int? ActualConsumableUnits { get; set; }

    [Column("actual_reusable_units")]
    public int? ActualReusableUnits { get; set; }

    [Column("drift_note")]
    public string? DriftNote { get; set; }

    [Column("total_consumable_rows")]
    public int TotalConsumableRows { get; set; }

    [Column("processed_consumable_rows")]
    public int ProcessedConsumableRows { get; set; }

    [Column("last_processed_inventory_id")]
    public int? LastProcessedInventoryId { get; set; }

    [Column("total_reusable_units")]
    public int TotalReusableUnits { get; set; }

    [Column("processed_reusable_units")]
    public int ProcessedReusableUnits { get; set; }

    [Column("last_processed_reusable_id")]
    public int? LastProcessedReusableId { get; set; }

    [Column("last_batch_at", TypeName = "timestamp with time zone")]
    public DateTime? LastBatchAt { get; set; }

    [Column("resolution_type")]
    [StringLength(50)]
    public string? ResolutionType { get; set; }

    [Column("target_depot_id")]
    public int? TargetDepotId { get; set; }

    [Column("external_note")]
    public string? ExternalNote { get; set; }

    [Column("consumable_zeroed")]
    public bool ConsumableZeroed { get; set; }

    [Column("reusable_zeroed")]
    public bool ReusableZeroed { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Column("max_retries")]
    public int MaxRetries { get; set; } = 5;

    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    [Column("completed_at", TypeName = "timestamp with time zone")]
    public DateTime? CompletedAt { get; set; }

    [Column("cancelled_by")]
    public Guid? CancelledBy { get; set; }

    [Column("cancelled_at", TypeName = "timestamp with time zone")]
    public DateTime? CancelledAt { get; set; }

    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

    [Column("is_forced")]
    public bool IsForced { get; set; }

    [Column("force_reason")]
    public string? ForceReason { get; set; }

    [Column("row_version")]
    public int RowVersion { get; set; } = 1;

    [ForeignKey("DepotId")]
    public virtual Depot? Depot { get; set; }

    [ForeignKey("TargetDepotId")]
    public virtual Depot? TargetDepot { get; set; }

    [InverseProperty(nameof(DepotClosureTransfer.Closure))]
    public virtual ICollection<DepotClosureTransfer> Transfers { get; set; } = new List<DepotClosureTransfer>();
}
