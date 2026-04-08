using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.System;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class SosRequestIncidentHelper
{
    public static async Task<IReadOnlyCollection<int>> MarkSosRequestsAsIncidentAsync(
        IEnumerable<int?> sosRequestIds,
        int teamIncidentId,
        int missionId,
        MissionTeamModel missionTeam,
        MissionActivityModel? activity,
        string incidentNote,
        Guid reportedBy,
        ISosRequestRepository sosRequestRepository,
        ISosPriorityRuleConfigRepository sosPriorityRuleConfigRepository,
        ISosRequestUpdateRepository sosRequestUpdateRepository,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var resolvedSosIds = sosRequestIds
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (resolvedSosIds.Count == 0)
        {
            return [];
        }

        var impactedSosIds = new List<int>();
        var incidentUpdates = new List<SosRequestIncidentUpdateModel>();
        var configModel = await sosPriorityRuleConfigRepository.GetAsync(cancellationToken);
        var config = SosPriorityRuleConfigSupport.FromModel(configModel);

        foreach (var sosRequestId in resolvedSosIds)
        {
            var sosRequest = await sosRequestRepository.GetByIdAsync(sosRequestId, cancellationToken);
            if (sosRequest is null)
            {
                logger.LogWarning("Skipping SOS incident mark because SosRequestId={SosRequestId} was not found.", sosRequestId);
                continue;
            }

            if (sosRequest.Status is SosRequestStatus.Resolved or SosRequestStatus.Cancelled)
            {
                logger.LogInformation(
                    "Skipping SOS incident mark for SosRequestId={SosRequestId} because status is {Status}.",
                    sosRequestId,
                    sosRequest.Status);
                continue;
            }

            sosRequest.ClusterId = null;
            var escalatedPriority = EscalatePriority(sosRequest.PriorityLevel);
            sosRequest.SetStatus(SosRequestStatus.Incident);
            sosRequest.SetPriorityLevel(escalatedPriority);
            sosRequest.SetPriorityScore(EscalatePriorityScore(
                sosRequest.PriorityScore,
                escalatedPriority,
                config));
            await sosRequestRepository.UpdateAsync(sosRequest, cancellationToken);

            incidentUpdates.Add(new SosRequestIncidentUpdateModel
            {
                SosRequestId = sosRequestId,
                TeamIncidentId = teamIncidentId,
                MissionId = missionId,
                MissionTeamId = missionTeam.Id,
                MissionActivityId = activity?.Id,
                IncidentScope = activity is null ? "Mission" : "Activity",
                Note = incidentNote.Trim(),
                ReportedById = reportedBy,
                CreatedAt = DateTime.UtcNow,
                TeamName = missionTeam.TeamName,
                ActivityType = activity?.ActivityType
            });

            impactedSosIds.Add(sosRequestId);
        }

        if (incidentUpdates.Count > 0)
        {
            await sosRequestUpdateRepository.AddIncidentRangeAsync(incidentUpdates, cancellationToken);
        }

        return impactedSosIds;
    }

    public static IEnumerable<int?> ResolveLifecycleSosRequestIds(IEnumerable<MissionActivityModel> activities) =>
        activities
            .Where(IsLifecycleActivity)
            .Select(activity => activity.SosRequestId);

    private static bool IsLifecycleActivity(MissionActivityModel activity) =>
        !string.Equals(activity.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase);

    private static SosPriorityLevel EscalatePriority(SosPriorityLevel? currentPriority) =>
        currentPriority switch
        {
            SosPriorityLevel.Low => SosPriorityLevel.Medium,
            SosPriorityLevel.Medium => SosPriorityLevel.High,
            SosPriorityLevel.High => SosPriorityLevel.Critical,
            SosPriorityLevel.Critical => SosPriorityLevel.Critical,
            _ => SosPriorityLevel.High
        };

    private static double EscalatePriorityScore(
        double? currentScore,
        SosPriorityLevel escalatedPriority,
        Domain.Entities.System.SosPriorityRuleConfigDocument config)
    {
        var minimumTargetScore = SosPriorityRuleConfigSupport.GetMinimumScoreForPriority(escalatedPriority, config);
        if (!currentScore.HasValue)
        {
            return minimumTargetScore > 0 ? minimumTargetScore : 1d;
        }

        return Math.Max(currentScore.Value + 1d, minimumTargetScore);
    }
}