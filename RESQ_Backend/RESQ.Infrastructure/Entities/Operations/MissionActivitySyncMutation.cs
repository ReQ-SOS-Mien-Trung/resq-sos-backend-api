using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Operations;

[Table("mission_activity_sync_mutations")]
public class MissionActivitySyncMutation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("client_mutation_id")]
    public Guid ClientMutationId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("mission_id")]
    public int MissionId { get; set; }

    [Column("activity_id")]
    public int ActivityId { get; set; }

    [Column("base_server_status")]
    [StringLength(50)]
    public string BaseServerStatus { get; set; } = string.Empty;

    [Column("requested_status")]
    [StringLength(50)]
    public string RequestedStatus { get; set; } = string.Empty;

    [Column("queued_at", TypeName = "timestamp with time zone")]
    public DateTimeOffset QueuedAt { get; set; }

    [Column("outcome")]
    [StringLength(50)]
    public string Outcome { get; set; } = string.Empty;

    [Column("effective_status")]
    [StringLength(50)]
    public string? EffectiveStatus { get; set; }

    [Column("current_server_status")]
    [StringLength(50)]
    public string? CurrentServerStatus { get; set; }

    [Column("error_code")]
    [StringLength(100)]
    public string? ErrorCode { get; set; }

    [Column("message")]
    public string? Message { get; set; }

    [Column("response_snapshot_json", TypeName = "jsonb")]
    public string ResponseSnapshotJson { get; set; } = "{}";

    [Column("processed_at", TypeName = "timestamp with time zone")]
    public DateTimeOffset ProcessedAt { get; set; }
}