using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Presentation.Hubs;

namespace RESQ.Presentation.Services;

public sealed class SosRequestRealtimeHubService(
    IHubContext<SosRequestHub> hubContext,
    ISosRequestSnapshotBuilder snapshotBuilder,
    ILogger<SosRequestRealtimeHubService> logger) : ISosRequestRealtimeHubService
{
    private const string EventSosRequestUpdate = "ReceiveSosRequestUpdate";

    public async Task PushSosRequestUpdateAsync(
        int sosRequestId,
        string action,
        int? previousClusterId = null,
        bool notifyUnclustered = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await snapshotBuilder.BuildAsync(sosRequestId, cancellationToken);
            if (snapshot is null)
            {
                logger.LogWarning(
                    "[SosRequestHub] Skip realtime update for missing SosRequestId={SosRequestId}",
                    sosRequestId);
                return;
            }

            var update = new SosRequestRealtimeUpdate
            {
                RequestId = snapshot.Id,
                Action = action,
                Status = snapshot.Status,
                PriorityLevel = snapshot.PriorityLevel,
                ClusterId = snapshot.ClusterId,
                PreviousClusterId = previousClusterId,
                ChangedAt = DateTime.UtcNow,
                Snapshot = snapshot
            };

            var groups = ResolveGroups(snapshot.Id, snapshot.ClusterId, previousClusterId, notifyUnclustered);
            await SendToGroupsAsync(groups, update, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "[SosRequestHub] Failed to push {Event} for SosRequestId={SosRequestId}",
                EventSosRequestUpdate,
                sosRequestId);
        }
    }

    public async Task PushSosRequestUpdatesAsync(
        IEnumerable<int> sosRequestIds,
        string action,
        int? previousClusterId = null,
        bool notifyUnclustered = false,
        CancellationToken cancellationToken = default)
    {
        var ids = sosRequestIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        foreach (var id in ids)
        {
            await PushSosRequestUpdateAsync(
                id,
                action,
                previousClusterId,
                notifyUnclustered,
                cancellationToken);
        }
    }

    private static HashSet<string> ResolveGroups(
        int requestId,
        int? clusterId,
        int? previousClusterId,
        bool notifyUnclustered)
    {
        var groups = new HashSet<string>(StringComparer.Ordinal)
        {
            SosRequestHub.AllGroup,
            SosRequestHub.RequestGroup(requestId)
        };

        if (clusterId.HasValue)
            groups.Add(SosRequestHub.ClusterGroup(clusterId.Value));

        if (previousClusterId.HasValue && previousClusterId != clusterId)
            groups.Add(SosRequestHub.ClusterGroup(previousClusterId.Value));

        if (!clusterId.HasValue || notifyUnclustered)
            groups.Add(SosRequestHub.UnclusteredGroup);

        return groups;
    }

    private async Task SendToGroupsAsync(
        IEnumerable<string> groups,
        SosRequestRealtimeUpdate update,
        CancellationToken cancellationToken)
    {
        var targetGroups = groups
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (targetGroups.Count == 0)
            return;

        await hubContext.Clients
            .Groups(targetGroups)
            .SendAsync(EventSosRequestUpdate, update, cancellationToken);
    }
}
