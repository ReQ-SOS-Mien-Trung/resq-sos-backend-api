using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.SyncMissionActivities;

public class SyncMissionActivitiesRequestDto
{
    public List<MissionActivitySyncItemDto> Items { get; set; } = [];
}

public class MissionActivitySyncItemDto
{
    public Guid ClientMutationId { get; set; }
    public int MissionId { get; set; }
    public int ActivityId { get; set; }
    public MissionActivityStatus TargetStatus { get; set; }
    public DateTimeOffset QueuedAt { get; set; }
    public MissionActivityStatus BaseServerStatus { get; set; }
    public string? ImageUrl { get; set; }
}
