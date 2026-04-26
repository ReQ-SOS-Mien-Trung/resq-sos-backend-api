using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class SosClusterStatusSyncHelper
{
    public static async Task SyncByClusterIdsAsync(
        IEnumerable<int> clusterIds,
        ISosClusterRepository sosClusterRepository,
        ISosRequestRepository sosRequestRepository,
        ILogger logger,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<int, SosRequestModel>? requestOverrides = null)
    {
        var affectedClusterIds = clusterIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (affectedClusterIds.Count == 0)
        {
            return;
        }

        foreach (var clusterId in affectedClusterIds)
        {
            var cluster = await sosClusterRepository.GetByIdAsync(clusterId, cancellationToken);
            if (cluster is null)
            {
                logger.LogWarning(
                    "Skipping SOS cluster status sync because ClusterId={ClusterId} was not found.",
                    clusterId);
                continue;
            }

            var clusterSosRequests = (await sosRequestRepository.GetByClusterIdAsync(clusterId, cancellationToken))
                .ToList();

            var effectiveSosRequests = ApplyOverrides(clusterId, clusterSosRequests, requestOverrides)
                .ToList();

            if (effectiveSosRequests.Count == 0)
            {
                continue;
            }

            if (!effectiveSosRequests.All(IsTerminalForClusterCompletion))
            {
                continue;
            }

            if (cluster.Status == SosClusterStatus.Completed)
            {
                continue;
            }

            cluster.Status = SosClusterStatus.Completed;
            cluster.LastUpdatedAt = DateTime.UtcNow;
            await sosClusterRepository.UpdateAsync(cluster, cancellationToken);

            logger.LogInformation(
                "Marked SosClusterId={ClusterId} as Completed because all SOS requests in the cluster are terminal.",
                clusterId);
        }
    }

    private static IEnumerable<SosRequestModel> ApplyOverrides(
        int clusterId,
        IEnumerable<SosRequestModel> clusterSosRequests,
        IReadOnlyDictionary<int, SosRequestModel>? requestOverrides)
    {
        if (requestOverrides is null || requestOverrides.Count == 0)
        {
            return clusterSosRequests;
        }

        var effectiveRequests = new Dictionary<int, SosRequestModel>();

        foreach (var sosRequest in clusterSosRequests)
        {
            if (requestOverrides.TryGetValue(sosRequest.Id, out var overrideRequest))
            {
                if (overrideRequest.ClusterId == clusterId)
                {
                    effectiveRequests[overrideRequest.Id] = overrideRequest;
                }

                continue;
            }

            effectiveRequests[sosRequest.Id] = sosRequest;
        }

        foreach (var overrideRequest in requestOverrides.Values)
        {
            if (overrideRequest.ClusterId == clusterId)
            {
                effectiveRequests[overrideRequest.Id] = overrideRequest;
            }
        }

        return effectiveRequests.Values;
    }

    private static bool IsTerminalForClusterCompletion(SosRequestModel sosRequest) =>
        sosRequest.Status is SosRequestStatus.Resolved or SosRequestStatus.Cancelled;
}
