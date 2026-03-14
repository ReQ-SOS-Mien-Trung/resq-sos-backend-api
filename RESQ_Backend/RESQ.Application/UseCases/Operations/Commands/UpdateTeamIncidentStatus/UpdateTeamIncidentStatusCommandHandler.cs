using MediatR;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateTeamIncidentStatus;

public class UpdateTeamIncidentStatusCommandHandler(
    ITeamIncidentRepository teamIncidentRepository,
    IMissionTeamRepository missionTeamRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork
) : IRequestHandler<UpdateTeamIncidentStatusCommand, UpdateTeamIncidentStatusResponse>
{
    public async Task<UpdateTeamIncidentStatusResponse> Handle(UpdateTeamIncidentStatusCommand request, CancellationToken cancellationToken)
    {
        var incident = await teamIncidentRepository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy sự cố với ID: {request.IncidentId}");

        TeamIncidentStateMachine.EnsureValidTransition(incident.Status, request.NewStatus);

        var finalStatus = request.NewStatus;

        // Acknowledged + NeedsAssistance decision
        if (incident.Status == TeamIncidentStatus.Acknowledged)
        {
            if (request.NeedsAssistance.HasValue)
                finalStatus = request.NeedsAssistance.Value ? TeamIncidentStatus.InProgress : TeamIncidentStatus.Closed;
        }

        await teamIncidentRepository.UpdateStatusAsync(request.IncidentId, finalStatus, cancellationToken);

        // Side-effects on rescue team when incident is resolved/closed
        if (finalStatus == TeamIncidentStatus.Resolved || finalStatus == TeamIncidentStatus.Closed)
        {
            var missionTeam = await missionTeamRepository.GetByIdAsync(incident.MissionTeamId, cancellationToken);
            if (missionTeam != null)
            {
                bool hasInjured = request.HasInjuredMember ?? false;
                string action = hasInjured ? "setunavailable" : "finish";
                try
                {
                    await mediator.Send(new ChangeTeamMissionStateCommand(missionTeam.RescuerTeamId, action), cancellationToken);
                }
                catch
                {
                    // Team state may already be in correct state — non-blocking
                }
            }
        }

        await unitOfWork.SaveAsync();

        return new UpdateTeamIncidentStatusResponse
        {
            IncidentId = request.IncidentId,
            Status = finalStatus.ToString()
        };
    }
}
