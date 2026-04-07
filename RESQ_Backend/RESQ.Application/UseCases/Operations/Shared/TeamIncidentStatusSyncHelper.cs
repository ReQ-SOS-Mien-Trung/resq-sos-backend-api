using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class TeamIncidentStatusSyncHelper
{
    public static async Task SyncBySosRequestIdsAsync(
        IEnumerable<int?> sosRequestIds,
        ISosRequestUpdateRepository sosRequestUpdateRepository,
        ISosRequestRepository sosRequestRepository,
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
        {
            return;
        }

        var incidentIdsBySos = await sosRequestUpdateRepository.GetTeamIncidentIdsBySosRequestIdsAsync(touchedSosIds, cancellationToken);
        var incidentIds = incidentIdsBySos
            .SelectMany(entry => entry.Value)
            .Distinct()
            .ToList();

        if (incidentIds.Count == 0)
        {
            return;
        }

        var sosIdsByIncident = await sosRequestUpdateRepository.GetSosRequestIdsByTeamIncidentIdsAsync(incidentIds, cancellationToken);

        foreach (var incidentId in incidentIds)
        {
            if (!sosIdsByIncident.TryGetValue(incidentId, out var linkedSosIds) || linkedSosIds.Count == 0)
            {
                continue;
            }

            var incident = await teamIncidentRepository.GetByIdAsync(incidentId, cancellationToken);
            if (incident is null || incident.Status == TeamIncidentStatus.Resolved)
            {
                continue;
            }

            var linkedSosRequests = (await Task.WhenAll(linkedSosIds
                    .Distinct()
                    .Select(id => sosRequestRepository.GetByIdAsync(id, cancellationToken))))
                .Where(sos => sos is not null)
                .ToList();

            if (linkedSosRequests.Count == 0)
            {
                continue;
            }

            var targetStatus = await ResolveTargetStatusAsync(
                incident.Status,
                linkedSosRequests.Select(sos => sos!).ToList(),
                missionActivityRepository,
                cancellationToken);

            if (!targetStatus.HasValue || targetStatus.Value == incident.Status)
            {
                continue;
            }

            await teamIncidentRepository.UpdateStatusAsync(incidentId, targetStatus.Value, cancellationToken);

            logger.LogInformation(
                "Synced TeamIncidentId={IncidentId} status from {CurrentStatus} to {TargetStatus} based on linked SOS requests [{SosRequestIds}]",
                incidentId,
                incident.Status,
                targetStatus.Value,
                string.Join(", ", linkedSosIds));
        }
    }

    private static async Task<TeamIncidentStatus?> ResolveTargetStatusAsync(
        TeamIncidentStatus currentStatus,
        IReadOnlyCollection<RESQ.Domain.Entities.Emergency.SosRequestModel> linkedSosRequests,
        IMissionActivityRepository missionActivityRepository,
        CancellationToken cancellationToken)
    {
        if (linkedSosRequests.All(IsProcessed))
        {
            return TeamIncidentStatus.Resolved;
        }

        if (currentStatus == TeamIncidentStatus.InProgress)
        {
            return null;
        }

        var relatedActivities = await missionActivityRepository.GetBySosRequestIdsAsync(
            linkedSosRequests.Select(sos => sos.Id),
            cancellationToken);

        var hasAssignedActivities = relatedActivities.Any(activity =>
            activity.SosRequestId.HasValue
            && activity.MissionTeamId.HasValue
            && !string.Equals(activity.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase));

        return hasAssignedActivities ? TeamIncidentStatus.InProgress : null;
    }

    private static bool IsProcessed(RESQ.Domain.Entities.Emergency.SosRequestModel sosRequest) =>
        sosRequest.Status is SosRequestStatus.Resolved or SosRequestStatus.Cancelled;
}