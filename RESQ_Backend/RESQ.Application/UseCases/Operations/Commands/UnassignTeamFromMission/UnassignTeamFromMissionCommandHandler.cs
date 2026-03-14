using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

namespace RESQ.Application.UseCases.Operations.Commands.UnassignTeamFromMission;

public class UnassignTeamFromMissionCommandHandler(
    IMissionTeamRepository missionTeamRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork
) : IRequestHandler<UnassignTeamFromMissionCommand, UnassignTeamFromMissionResponse>
{
    public async Task<UnassignTeamFromMissionResponse> Handle(UnassignTeamFromMissionCommand request, CancellationToken cancellationToken)
    {
        var missionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy liên kết đội-mission với ID: {request.MissionTeamId}");

        // Cancel the rescue team's mission assignment
        await mediator.Send(new ChangeTeamMissionStateCommand(missionTeam.RescuerTeamId, "cancel"), cancellationToken);

        await missionTeamRepository.DeleteAsync(request.MissionTeamId, cancellationToken);
        await unitOfWork.SaveAsync();

        return new UnassignTeamFromMissionResponse
        {
            MissionTeamId = request.MissionTeamId
        };
    }
}
