using MediatR;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMission;

public record UpdateMissionCommand(
    int MissionId,
    string? MissionType,
    double? PriorityScore,
    DateTime? StartTime,
    DateTime? ExpectedEndTime,
    Guid? UpdatedBy,
    IReadOnlyList<UpdateMissionActivityPatch> Activities
) : IRequest<MissionDto>;

public record UpdateMissionActivityPatch(
    int ActivityId,
    int? Step,
    string? Description,
    string? Target,
    double? TargetLatitude,
    double? TargetLongitude,
    List<SupplyToCollectDto>? Items);
