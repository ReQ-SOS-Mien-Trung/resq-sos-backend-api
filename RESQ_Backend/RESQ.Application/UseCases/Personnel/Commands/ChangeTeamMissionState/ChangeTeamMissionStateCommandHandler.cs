using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Handlers;

public class ChangeTeamMissionStateCommandHandler(
    IRescueTeamRepository teamRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ChangeTeamMissionStateCommand>
{
    public async Task Handle(ChangeTeamMissionStateCommand request, CancellationToken ct)
    {
        var team = await teamRepository.GetByIdAsync(request.TeamId, ct) 
            ?? throw new NotFoundException($"Không tìm thấy đội id = {request.TeamId}");

        switch (request.Action.ToLower())
        {
            case "assign": team.AssignMission(); break;
            case "cancel": team.CancelMission(); break;
            case "start": team.StartMission(); break;
            case "finish": team.FinishMission(); break;
            case "reportincident": team.ReportIncident(); break;
            case "setunavailable": team.SetUnavailable(); break;
            default: throw new BadRequestException("Action không hợp lệ.");
        }

        await teamRepository.UpdateAsync(team, ct);
        await unitOfWork.SaveAsync();
    }
}
