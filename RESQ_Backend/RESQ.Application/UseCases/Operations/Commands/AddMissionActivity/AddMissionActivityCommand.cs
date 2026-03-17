using MediatR;
using RESQ.Application.UseCases.Operations.Commands.CreateMission;

namespace RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;

public record AddMissionActivityCommand(
    int MissionId,
    int? Step,
    string? ActivityCode,
    string? ActivityType,
    string? Description,
    string? Priority,
    int? EstimatedTime,
    int? SosRequestId,
    int? DepotId,
    string? DepotName,
    string? DepotAddress,
    List<SuggestedSupplyItemDto>? SuppliesToCollect,
    string? Target,
    double? TargetLatitude,
    double? TargetLongitude,
    int? RescueTeamId,
    Guid AssignedById
) : IRequest<AddMissionActivityResponse>;
