using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToMission;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;

public class AssignTeamToActivityCommandHandler(
    IMissionActivityRepository activityRepository,
    IMissionTeamRepository missionTeamRepository,
    ISosRequestRepository sosRequestRepository,
    ISosClusterRepository sosClusterRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    ITeamIncidentRepository teamIncidentRepository,
    IOperationalHubService operationalHubService,
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
            // Team not assigned to the mission yet - assign it first
            var assignResult = await mediator.Send(
                new AssignTeamToMissionCommand(request.MissionId, request.RescueTeamId, request.AssignedById),
                cancellationToken);
            missionTeamId = assignResult.MissionTeamId;
            teamName = null;
        }

        await activityRepository.AssignTeamAsync(request.ActivityId, missionTeamId, cancellationToken);
        await unitOfWork.SaveAsync();

        if (activity.SosRequestId.HasValue)
        {
            var missionActivities = (await activityRepository.GetByMissionIdAsync(request.MissionId, cancellationToken))
                .ToList();
            var assignedActivity = missionActivities.FirstOrDefault(item => item.Id == activity.Id);
            if (assignedActivity is not null)
            {
                assignedActivity.MissionTeamId = missionTeamId;
            }
            else
            {
                activity.MissionTeamId = missionTeamId;
                missionActivities.Add(activity);
            }

            await MissionActivitySosRequestSyncHelper.SyncTouchedSosRequestsAsync(
                [activity.SosRequestId],
                missionActivities,
                sosRequestRepository,
                sosClusterRepository,
                sosRequestUpdateRepository,
                activityRepository,
                teamIncidentRepository,
                logger,
                cancellationToken);
            await unitOfWork.SaveAsync();
        }

        if (activity.DepotId.HasValue
            && IsRealtimeDepotActivity(activity.ActivityType))
        {
            await operationalHubService.PushDepotActivityUpdateAsync(
                new DepotActivityRealtimeUpdate
                {
                    ActivityId = activity.Id,
                    DepotId = activity.DepotId.Value,
                    MissionId = activity.MissionId,
                    MissionTeamId = missionTeamId,
                    RescueTeamId = request.RescueTeamId,
                    ActivityType = activity.ActivityType,
                    Action = "Assigned",
                    Status = activity.Status.ToString(),
                    EstimatedTime = activity.EstimatedTime
                },
                cancellationToken);
        }

        return new AssignTeamToActivityResponse
        {
            ActivityId = request.ActivityId,
            MissionTeamId = missionTeamId,
            RescueTeamId = request.RescueTeamId,
            TeamName = teamName
        };
    }

    private static bool IsRealtimeDepotActivity(string? activityType) =>
        string.Equals(activityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
        || string.Equals(activityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase);
}
