using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.SyncMissionActivities;

public record SyncMissionActivitiesCommand(
    Guid UserId,
    IReadOnlyList<MissionActivitySyncItemDto> Items
) : IRequest<SyncMissionActivitiesResponseDto>;
