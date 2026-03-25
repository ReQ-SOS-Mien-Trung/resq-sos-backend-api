using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Notifications;

[Table("depot_realtime_outbox")]
public class DepotRealtimeOutbox
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("depot_id")]
    public int DepotId { get; set; }

    [Column("mission_id")]
    public int? MissionId { get; set; }

    [Column("version")]
    public long Version { get; set; }

    [Column("event_type")]
    [MaxLength(120)]
    public string EventType { get; set; } = "DepotUpdated";

    [Column("operation")]
    [MaxLength(40)]
    public string Operation { get; set; } = "Update";

    [Column("payload_kind")]
    [MaxLength(20)]
    public string PayloadKind { get; set; } = "Full";

    [Column("is_critical")]
    public bool IsCritical { get; set; }

    [Column("changed_fields")]
    public string? ChangedFields { get; set; }

    [Column("snapshot_payload")]
    public string? SnapshotPayload { get; set; }

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [Column("attempt_count")]
    public int AttemptCount { get; set; }

    [Column("next_attempt_at", TypeName = "timestamp with time zone")]
    public DateTime NextAttemptAt { get; set; }

    [Column("occurred_at", TypeName = "timestamp with time zone")]
    public DateTime OccurredAt { get; set; }

    [Column("lock_owner")]
    [MaxLength(120)]
    public string? LockOwner { get; set; }

    [Column("lock_expires_at", TypeName = "timestamp with time zone")]
    public DateTime? LockExpiresAt { get; set; }

    [Column("last_error")]
    public string? LastError { get; set; }

    [Column("processed_at", TypeName = "timestamp with time zone")]
    public DateTime? ProcessedAt { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; }
}
