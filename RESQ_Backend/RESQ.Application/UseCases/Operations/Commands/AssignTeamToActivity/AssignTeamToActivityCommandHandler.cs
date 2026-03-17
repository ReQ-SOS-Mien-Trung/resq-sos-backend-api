using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToMission;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;

public class AssignTeamToActivityCommandHandler(
    IMissionActivityRepository activityRepository,
    IMissionTeamRepository missionTeamRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ILogger<AssignTeamToActivityCommandHandler> logger
) : IRequestHandler<AssignTeamToActivityCommand, AssignTeamToActivityResponse>
{
    public async Task<AssignTeamToActivityResponse> Handle(AssignTeamToActivityCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Assigning RescueTeamId={teamId} to ActivityId={activityId}", request.RescueTeamId, request.ActivityId);

        var activity = await activityRepository.GetByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy activity với ID: {request.ActivityId}");

        // Look up existing MissionTeam record for this mission + rescue team
        var missionTeam = await missionTeamRepository.GetByMissionAndTeamAsync(request.MissionId, request.RescueTeamId, cancellationToken);

        int missionTeamId;
        string? teamName;
        if (missionTeam is not null)
        {
            missionTeamId = missionTeam.Id;
            teamName = missionTeam.TeamName;
        }
        else
        {
            // Team not assigned to the mission yet — assign it first
            var assignResult = await mediator.Send(
                new AssignTeamToMissionCommand(request.MissionId, request.RescueTeamId, request.AssignedById),
                cancellationToken);
            missionTeamId = assignResult.MissionTeamId;
            teamName = null;
        }

        await activityRepository.AssignTeamAsync(request.ActivityId, missionTeamId, cancellationToken);
        await unitOfWork.SaveAsync();

        return new AssignTeamToActivityResponse
        {
            ActivityId = request.ActivityId,
            MissionTeamId = missionTeamId,
            RescueTeamId = request.RescueTeamId,
            TeamName = teamName
        };
    }
}
