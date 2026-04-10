using MediatR;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionPendingActivities;

public record UpdateMissionPendingActivitiesCommand(
    int MissionId,
    Guid UpdatedBy,
    IReadOnlyList<UpdateMissionPendingActivityPatch> Activities
) : IRequest<UpdateMissionPendingActivitiesResponse>;

public record UpdateMissionPendingActivityPatch(
    int ActivityId,
    int? Step,
    string? Description,
    string? Target,
    double? TargetLatitude,
    double? TargetLongitude,
    List<SupplyToCollectDto>? Items);