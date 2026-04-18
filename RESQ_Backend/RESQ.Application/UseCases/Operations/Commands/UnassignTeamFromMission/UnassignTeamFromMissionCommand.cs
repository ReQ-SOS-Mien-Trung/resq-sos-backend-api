using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.UnassignTeamFromMission;

public record UnassignTeamFromMissionCommand(
    int MissionTeamId,
    Guid RequestedById
) : IRequest<UnassignTeamFromMissionResponse>;
