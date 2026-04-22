using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

public interface IMissionActivityStatusExecutionService
{
    Task<MissionActivityStatusExecutionResult> ApplyAsync(
        int expectedMissionId,
        int activityId,
        MissionActivityStatus requestedStatus,
        Guid decisionBy,
        string? imageUrl = null,
        CancellationToken cancellationToken = default);
}

public sealed class MissionActivityStatusExecutionResult
{
    public required MissionActivityStatus EffectiveStatus { get; init; }
    public required MissionActivityStatus? CurrentServerStatus { get; init; }
    public string? ImageUrl { get; init; }
    public int ActivityId { get; init; }
    public int? MissionId { get; init; }
    public int? DepotId { get; init; }
    public int? MissionTeamId { get; init; }
    public int? RescueTeamId { get; init; }
    public string? ActivityType { get; init; }
    public int? EstimatedTime { get; init; }
    public bool InventoryChanged { get; init; }
    public List<SupplyExecutionItemDto> ConsumedItems { get; init; } = [];
}
