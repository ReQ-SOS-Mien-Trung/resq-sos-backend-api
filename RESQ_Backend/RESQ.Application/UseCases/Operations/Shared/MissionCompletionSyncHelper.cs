using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class MissionCompletionSyncHelper
{
    public static async Task<bool> TryCompleteMissionIfReadyAsync(
        int missionId,
        IMissionRepository missionRepository,
        IMissionActivityRepository missionActivityRepository,
        ISosClusterRepository sosClusterRepository,
        ISosRequestRepository sosRequestRepository,
        ISosRequestUpdateRepository sosRequestUpdateRepository,
        ITeamIncidentRepository teamIncidentRepository,
        ILogger logger,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken,
        MissionModel? missionSnapshot = null,
        ICollection<int>? resolvedSosRequestIds = null)
    {
        var mission = missionSnapshot ?? await missionRepository.GetByIdAsync(missionId, cancellationToken);
        if (mission is null || mission.Status != MissionStatus.OnGoing || mission.IsCompleted == true)
        {
            return false;
        }

        var activities = missionSnapshot is not null
            ? missionSnapshot.Activities.ToList()
            : (await missionActivityRepository.GetByMissionIdAsync(missionId, cancellationToken)).ToList();

        if (activities.Count == 0 || !activities.All(IsActivitySettledForMissionCompletion))
        {
            return false;
        }

        await missionRepository.UpdateStatusAsync(missionId, MissionStatus.Completed, isCompleted: true, cancellationToken);
        mission.Status = MissionStatus.Completed;
        mission.IsCompleted = true;
        mission.CompletedAt ??= DateTime.UtcNow;

        if (mission.ClusterId.HasValue)
        {
            var cluster = await sosClusterRepository.GetByIdAsync(mission.ClusterId.Value, cancellationToken);
            if (cluster is not null)
            {
                cluster.Status = SosClusterStatus.Completed;
                await sosClusterRepository.UpdateAsync(cluster, cancellationToken);
            }

            await sosRequestRepository.UpdateStatusByClusterIdAsync(
                mission.ClusterId.Value,
                SosRequestStatus.Resolved,
                cancellationToken);

            // Incident sync reads SOS requests through no-tracking queries, so persist these status changes first.
            await unitOfWork.SaveAsync();

            var clusterSosRequests = (await sosRequestRepository.GetByClusterIdAsync(
                mission.ClusterId.Value,
                cancellationToken)).ToList();

            foreach (var sos in clusterSosRequests)
            {
                resolvedSosRequestIds?.Add(sos.Id);
            }

            await TeamIncidentStatusSyncHelper.SyncBySosRequestIdsAsync(
                clusterSosRequests.Select(sos => (int?)sos.Id),
                sosRequestUpdateRepository,
                sosRequestRepository,
                missionActivityRepository,
                teamIncidentRepository,
                logger,
                cancellationToken);
        }

        await unitOfWork.SaveAsync();

        logger.LogInformation(
            "MissionId={MissionId} completed after all activities settled.",
            missionId);

        return true;
    }

    private static bool IsActivitySettledForMissionCompletion(MissionActivityModel activity) =>
        activity.Status is MissionActivityStatus.Succeed
            or MissionActivityStatus.Failed
            or MissionActivityStatus.Cancelled;
}
