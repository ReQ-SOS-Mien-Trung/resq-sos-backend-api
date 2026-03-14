using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.AssignTeamToMission;

public record AssignTeamToMissionCommand(
    int MissionId,
    int RescueTeamId,
    string? TeamType,
    string? Note,
    Guid AssignedById
) : IRequest<AssignTeamToMissionResponse>;
