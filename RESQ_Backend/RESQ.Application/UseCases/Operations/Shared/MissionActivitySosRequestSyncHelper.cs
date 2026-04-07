using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class MissionActivitySosRequestSyncHelper
{
    private static readonly HashSet<MissionActivityStatus> OpenStatuses =
    [
        MissionActivityStatus.Planned,
        MissionActivityStatus.OnGoing,
        MissionActivityStatus.PendingConfirmation
    ];

    public static async Task SyncTouchedSosRequestsAsync(
        IEnumerable<int?> sosRequestIds,
        IEnumerable<MissionActivityModel> missionActivities,
        ISosRequestRepository sosRequestRepository,
        ISosRequestUpdateRepository sosRequestUpdateRepository,
        IMissionActivityRepository missionActivityRepository,
        ITeamIncidentRepository teamIncidentRepository,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var touchedSosIds = sosRequestIds
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (touchedSosIds.Count == 0)
            return;

        var activitySnapshot = missionActivities.ToList();

        foreach (var sosRequestId in touchedSosIds)
        {
            var relatedActivities = activitySnapshot
                .Where(activity => activity.SosRequestId == sosRequestId && IsLifecycleActivity(activity))
                .ToList();

            if (relatedActivities.Count == 0)
                continue;

            var nonCancelledActivities = relatedActivities
                .Where(activity => activity.Status != MissionActivityStatus.Cancelled)
                .ToList();

            if (nonCancelledActivities.Count == 0)
                continue;

            var sosRequest = await sosRequestRepository.GetByIdAsync(sosRequestId, cancellationToken);
            if (sosRequest is null)
            {
                logger.LogWarning(
                    "Skipping SOS lifecycle sync because SosRequestId={SosRequestId} was not found.",
                    sosRequestId);
                continue;
            }

            if (sosRequest.Status == SosRequestStatus.Incident)
            {
                logger.LogInformation(
                    "Skipping SOS lifecycle sync for SosRequestId={SosRequestId} because it is explicitly in Incident state.",
                    sosRequestId);
                continue;
            }

            var hasOpenActivities = nonCancelledActivities.Any(activity => OpenStatuses.Contains(activity.Status));
            var allActivitiesSucceeded = nonCancelledActivities.All(activity => activity.Status == MissionActivityStatus.Succeed);
            var hasFailedActivity = nonCancelledActivities.Any(activity => activity.Status == MissionActivityStatus.Failed);

            if (allActivitiesSucceeded)
            {
                if (sosRequest.Status == SosRequestStatus.Resolved)
                    continue;

                sosRequest.SetStatus(SosRequestStatus.Resolved);
                await sosRequestRepository.UpdateAsync(sosRequest, cancellationToken);

                logger.LogInformation(
                    "Marked SosRequestId={SosRequestId} as Resolved because all related mission activities succeeded.",
                    sosRequestId);

                continue;
            }

            if (!hasFailedActivity || hasOpenActivities)
                continue;

            var escalatedPriority = EscalatePriority(sosRequest.PriorityLevel, logger, sosRequestId);
            var requiresUpdate = sosRequest.Status != SosRequestStatus.Pending
                || sosRequest.ClusterId.HasValue
                || sosRequest.PriorityLevel != escalatedPriority;

            if (!requiresUpdate)
                continue;

            sosRequest.ClusterId = null;
            sosRequest.SetStatus(SosRequestStatus.Pending);
            sosRequest.SetPriorityLevel(escalatedPriority);

            await sosRequestRepository.UpdateAsync(sosRequest, cancellationToken);

            logger.LogInformation(
                "Reopened SosRequestId={SosRequestId} for re-clustering after related mission activity failure. NewPriority={Priority}.",
                sosRequestId,
                escalatedPriority);
        }

        await TeamIncidentStatusSyncHelper.SyncBySosRequestIdsAsync(
            touchedSosIds.Select(id => (int?)id),
            sosRequestUpdateRepository,
            sosRequestRepository,
            missionActivityRepository,
            teamIncidentRepository,
            logger,
            cancellationToken);
    }

    private static bool IsLifecycleActivity(MissionActivityModel activity) =>
        !string.Equals(activity.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase);

    private static SosPriorityLevel EscalatePriority(
        SosPriorityLevel? currentPriority,
        ILogger logger,
        int sosRequestId)
    {
        if (!currentPriority.HasValue)
        {
            logger.LogWarning(
                "SosRequestId={SosRequestId} has no current priority. Defaulting reopened priority to High.",
                sosRequestId);

            return SosPriorityLevel.High;
        }

        return currentPriority.Value switch
        {
            SosPriorityLevel.Low => SosPriorityLevel.Medium,
            SosPriorityLevel.Medium => SosPriorityLevel.High,
            SosPriorityLevel.High => SosPriorityLevel.Critical,
            SosPriorityLevel.Critical => SosPriorityLevel.Critical,
            _ => SosPriorityLevel.Critical
        };
    }
}