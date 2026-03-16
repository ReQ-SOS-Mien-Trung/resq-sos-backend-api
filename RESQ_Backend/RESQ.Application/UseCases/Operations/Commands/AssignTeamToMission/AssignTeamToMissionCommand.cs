using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.AssignTeamToMission;

public record AssignTeamToMissionCommand(
    int MissionId,
    int RescueTeamId,
    Guid AssignedById
) : IRequest<AssignTeamToMissionResponse>;
