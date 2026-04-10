using RESQ.Domain.Enum.Operations;

namespace RESQ.Domain.Entities.Operations;

public class MissionActivitySyncMutationModel
{
    public int Id { get; set; }
    public Guid ClientMutationId { get; set; }
    public Guid UserId { get; set; }
    public int MissionId { get; set; }
    public int ActivityId { get; set; }
    public MissionActivityStatus BaseServerStatus { get; set; }
    public MissionActivityStatus RequestedStatus { get; set; }
    public DateTimeOffset QueuedAt { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public MissionActivityStatus? EffectiveStatus { get; set; }
    public MissionActivityStatus? CurrentServerStatus { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public string ResponseSnapshotJson { get; set; } = "{}";
    public DateTimeOffset ProcessedAt { get; set; }
}