using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("depot_supply_requests")]
public partial class DepotSupplyRequest
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Kho yêu cầu tiếp tế (kho của manager tạo request).</summary>
    [Column("requesting_depot_id")]
    public int RequestingDepotId { get; set; }

    /// <summary>Kho được yêu cầu tiếp tế (kho nguồn).</summary>
    [Column("source_depot_id")]
    public int SourceDepotId { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("priority_level")]
    [StringLength(20)]
    public string PriorityLevel { get; set; } = "Medium";

    /// <summary>Trạng thái từ góc nhìn kho nguồn.</summary>
    [Column("source_status")]
    [StringLength(50)]
    public string SourceStatus { get; set; } = "Pending";

    /// <summary>Trạng thái từ góc nhìn kho yêu cầu.</summary>
    [Column("requesting_status")]
    [StringLength(50)]
    public string RequestingStatus { get; set; } = "WaitingForApproval";

    [Column("rejected_reason")]
    public string? RejectedReason { get; set; }

    [Column("requested_by")]
    public Guid RequestedBy { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("auto_reject_at", TypeName = "timestamp with time zone")]
    public DateTime? AutoRejectAt { get; set; }

    [Column("high_escalation_notified")]
    public bool HighEscalationNotified { get; set; }

    [Column("high_escalation_notified_at", TypeName = "timestamp with time zone")]
    public DateTime? HighEscalationNotifiedAt { get; set; }

    [Column("urgent_escalation_notified")]
    public bool UrgentEscalationNotified { get; set; }

    [Column("urgent_escalation_notified_at", TypeName = "timestamp with time zone")]
    public DateTime? UrgentEscalationNotifiedAt { get; set; }

    [Column("responded_at", TypeName = "timestamp with time zone")]
    public DateTime? RespondedAt { get; set; }

    [Column("shipped_at", TypeName = "timestamp with time zone")]
    public DateTime? ShippedAt { get; set; }

    [Column("completed_at", TypeName = "timestamp with time zone")]
    public DateTime? CompletedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("RequestingDepotId")]
    [InverseProperty("SupplyRequestsAsRequester")]
    public virtual Depot RequestingDepot { get; set; } = null!;

    [ForeignKey("SourceDepotId")]
    [InverseProperty("SupplyRequestsAsSource")]
    public virtual Depot SourceDepot { get; set; } = null!;

    [ForeignKey("RequestedBy")]
    [InverseProperty("DepotSupplyRequests")]
    public virtual User RequestedByUser { get; set; } = null!;

    [InverseProperty("DepotSupplyRequest")]
    public virtual ICollection<DepotSupplyRequestItem> Items { get; set; } = new List<DepotSupplyRequestItem>();
}
