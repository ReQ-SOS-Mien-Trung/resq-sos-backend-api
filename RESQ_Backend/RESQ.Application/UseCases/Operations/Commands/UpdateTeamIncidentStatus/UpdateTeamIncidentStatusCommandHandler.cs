using MediatR;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands; // ResolveIncidentCommand
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

        var finalStatus = request.NewStatus;

        // Reported + NeedsAssistance decision
        if (incident.Status == TeamIncidentStatus.Reported)
        {
            if (request.NeedsAssistance.HasValue)
                finalStatus = request.NeedsAssistance.Value ? TeamIncidentStatus.InProgress : TeamIncidentStatus.Closed;
        }

        TeamIncidentStateMachine.EnsureValidTransition(incident.Status, finalStatus);

        await teamIncidentRepository.UpdateStatusAsync(request.IncidentId, finalStatus, cancellationToken);

        // Side-effects on rescue team when incident is resolved/closed
        if (finalStatus == TeamIncidentStatus.Resolved || finalStatus == TeamIncidentStatus.Closed)
        {
            var missionTeam = await missionTeamRepository.GetByIdAsync(incident.MissionTeamId, cancellationToken);
            if (missionTeam != null)
            {
                bool hasInjured = request.HasInjuredMember ?? false;
                try
                {
                    // Team is in Stuck state after reporting incident; ResolveIncident transitions Stuck → Available/Unavailable
                    await mediator.Send(new ResolveIncidentCommand(missionTeam.RescuerTeamId, hasInjured), cancellationToken);
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
