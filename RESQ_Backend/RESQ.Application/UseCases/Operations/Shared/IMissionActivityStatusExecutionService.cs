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
        CancellationToken cancellationToken = default);
}

public sealed class MissionActivityStatusExecutionResult
{
    public required MissionActivityStatus EffectiveStatus { get; init; }
    public required MissionActivityStatus? CurrentServerStatus { get; init; }
    public List<SupplyExecutionItemDto> ConsumedItems { get; init; } = [];
}