using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.SyncMissionActivities;

public class SyncMissionActivitiesResponseDto
{
    public MissionActivitySyncSummaryDto Summary { get; set; } = new();
    public List<MissionActivitySyncResultDto> Results { get; set; } = [];
}

public class MissionActivitySyncSummaryDto
{
    public int Total { get; set; }
    public int Applied { get; set; }
    public int Duplicate { get; set; }
    public int Conflict { get; set; }
    public int Rejected { get; set; }
    public int Failed { get; set; }
}

public class MissionActivitySyncResultDto
{
    public Guid ClientMutationId { get; set; }
    public int MissionId { get; set; }
    public int ActivityId { get; set; }
    public MissionActivityStatus TargetStatus { get; set; }
    public MissionActivityStatus BaseServerStatus { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public MissionActivityStatus? EffectiveStatus { get; set; }
    public MissionActivityStatus? CurrentServerStatus { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public string? ImageUrl { get; set; }
    public List<SupplyExecutionItemDto> ConsumedItems { get; set; } = [];
}
