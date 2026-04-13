using Microsoft.Extensions.Logging;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class MissionActivityAutoStartHelper
{
    public static async Task<IReadOnlyCollection<int>> AutoStartFirstActivitiesPerTeamAsync(
        int missionId,
        Guid decisionBy,
        IMissionActivityRepository activityRepository,
        IMissionTeamRepository missionTeamRepository,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var missionActivities = (await activityRepository.GetByMissionIdAsync(missionId, cancellationToken)).ToList();
        var startedActivityIds = new List<int>();

        foreach (var activity in MissionActivitySequenceHelper.GetFirstPlannedActivitiesPerTeam(missionActivities))
        {
            if (!activity.MissionTeamId.HasValue)
            {
                logger.LogWarning(
                    "Skipped auto-start for ActivityId={ActivityId} in MissionId={MissionId} because the activity is not assigned to a mission team.",
                    activity.Id,
                    missionId);
                continue;
            }

            if (MissionActivitySequenceHelper.HasActiveActivityForTeam(missionActivities, activity.MissionTeamId.Value))
            {
                logger.LogInformation(
                    "Skipped auto-start of first activity for MissionTeamId={MissionTeamId} in MissionId={MissionId} because the team already has an active activity.",
                    activity.MissionTeamId.Value,
                    missionId);
                continue;
            }

            if (await TryStartActivityAsync(activity, decisionBy, activityRepository, missionTeamRepository, logger, cancellationToken))
            {
                startedActivityIds.Add(activity.Id);
                activity.Status = MissionActivityStatus.OnGoing;
            }
        }

        return startedActivityIds;
    }

    public static async Task<int?> AutoStartNextActivityForSameTeamAsync(
        MissionActivityModel currentActivity,
        Guid decisionBy,
        IMissionActivityRepository activityRepository,
        IMissionTeamRepository missionTeamRepository,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!currentActivity.MissionId.HasValue || !currentActivity.MissionTeamId.HasValue)
            return null;

        var missionActivities = (await activityRepository.GetByMissionIdAsync(currentActivity.MissionId.Value, cancellationToken)).ToList();
        var missionTeamId = currentActivity.MissionTeamId.Value;

        if (MissionActivitySequenceHelper.HasActiveActivityForTeam(missionActivities, missionTeamId))
        {
            logger.LogInformation(
                "Skipped auto-start of next activity for MissionTeamId={MissionTeamId} because the team already has another active activity.",
                missionTeamId);
            return null;
        }

        var nextActivity = MissionActivitySequenceHelper.GetNextPlannedActivityForSameTeam(missionActivities, currentActivity);
        if (nextActivity is null)
            return null;

        var started = await TryStartActivityAsync(
            nextActivity,
            decisionBy,
            activityRepository,
            missionTeamRepository,
            logger,
            cancellationToken);

        return started ? nextActivity.Id : null;
    }

    private static async Task<bool> TryStartActivityAsync(
        MissionActivityModel activity,
        Guid decisionBy,
        IMissionActivityRepository activityRepository,
        IMissionTeamRepository missionTeamRepository,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!activity.MissionTeamId.HasValue)
            return false;

        MissionActivityStateMachine.EnsureValidTransition(activity.Status, MissionActivityStatus.OnGoing);
        await activityRepository.UpdateStatusAsync(activity.Id, MissionActivityStatus.OnGoing, decisionBy, cancellationToken: cancellationToken);

        var assignedMissionTeam = await missionTeamRepository.GetByIdAsync(activity.MissionTeamId.Value, cancellationToken);
        if (assignedMissionTeam is not null
            && string.Equals(assignedMissionTeam.Status, MissionTeamExecutionStatus.Assigned.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            await missionTeamRepository.UpdateStatusAsync(
                assignedMissionTeam.Id,
                MissionTeamExecutionStatus.InProgress.ToString(),
                cancellationToken);
        }

        logger.LogInformation(
            "Auto-started ActivityId={ActivityId} Step={Step} for MissionTeamId={MissionTeamId}.",
            activity.Id,
            activity.Step,
            activity.MissionTeamId.Value);

        return true;
    }
}

internal static class MissionActivitySequenceHelper
{
    public static IEnumerable<MissionActivityModel> GetFirstPlannedActivitiesPerTeam(IEnumerable<MissionActivityModel> activities) =>
        activities
            .Where(a => a.MissionTeamId.HasValue)
            .GroupBy(a => a.MissionTeamId!.Value)
            .Select(group => OrderActivities(group).FirstOrDefault(a => a.Status == MissionActivityStatus.Planned))
            .Where(activity => activity is not null)
            .Cast<MissionActivityModel>();

    public static MissionActivityModel? GetNextPlannedActivityForSameTeam(
        IEnumerable<MissionActivityModel> activities,
        MissionActivityModel currentActivity)
    {
        if (!currentActivity.MissionTeamId.HasValue)
            return null;

        var orderedActivities = activities
            .Where(a => a.MissionTeamId == currentActivity.MissionTeamId)
            .OrderBy(a => a.Step ?? int.MaxValue)
            .ThenBy(a => a.Id)
            .ToList();

        var currentIndex = orderedActivities.FindIndex(a => a.Id == currentActivity.Id);
        if (currentIndex < 0)
            return null;

        return orderedActivities
            .Skip(currentIndex + 1)
            .FirstOrDefault(a => a.Status == MissionActivityStatus.Planned);
    }

    public static MissionActivityModel? GetEarliestUnfinishedActivityForSameTeam(
        IEnumerable<MissionActivityModel> activities,
        MissionActivityModel currentActivity)
    {
        if (!currentActivity.MissionTeamId.HasValue)
            return null;

        return OrderActivities(activities.Where(a => a.MissionTeamId == currentActivity.MissionTeamId))
            .FirstOrDefault(a => !IsTerminalStatus(a.Status));
    }

    public static bool HasActiveActivityForTeam(
        IEnumerable<MissionActivityModel> activities,
        int missionTeamId,
        int? excludeActivityId = null) =>
        activities.Any(a => a.MissionTeamId == missionTeamId
            && a.Id != excludeActivityId
            && a.Status is MissionActivityStatus.OnGoing or MissionActivityStatus.PendingConfirmation);

    public static bool IsTerminalStatus(MissionActivityStatus status) =>
        status is MissionActivityStatus.Succeed or MissionActivityStatus.Failed or MissionActivityStatus.Cancelled;

    private static IOrderedEnumerable<MissionActivityModel> OrderActivities(IEnumerable<MissionActivityModel> activities) =>
        activities.OrderBy(a => a.Step ?? int.MaxValue)
            .ThenBy(a => a.Id);
}
