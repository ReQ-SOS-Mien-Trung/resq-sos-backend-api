using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;

public class ReportTeamIncidentCommandHandler(
    IMissionTeamRepository missionTeamRepository,
    ITeamIncidentRepository teamIncidentRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork
) : IRequestHandler<ReportTeamIncidentCommand, ReportTeamIncidentResponse>
{
    public async Task<ReportTeamIncidentResponse> Handle(ReportTeamIncidentCommand request, CancellationToken cancellationToken)
    {
        var missionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy liên kết đội-mission với ID: {request.MissionTeamId}");

        var now = DateTime.UtcNow;
        var incident = new TeamIncidentModel
        {
            MissionTeamId = request.MissionTeamId,
            Description = request.Description,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Status = TeamIncidentStatus.Reported,
            ReportedBy = request.ReportedBy,
            ReportedAt = now
        };

        var incidentId = await teamIncidentRepository.CreateAsync(incident, cancellationToken);

        // Update rescue team status: OnMission → Stuck
        await mediator.Send(new ChangeTeamMissionStateCommand(missionTeam.RescuerTeamId, "reportincident"), cancellationToken);

        await unitOfWork.SaveAsync();

        return new ReportTeamIncidentResponse
        {
            IncidentId = incidentId,
            MissionTeamId = request.MissionTeamId,
            Status = "Reported",
            ReportedAt = now
        };
    }
}
