using System.Text.Json;
using Microsoft.Extensions.Logging;
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
        var priorityThresholds = await LoadPriorityThresholdsAsync(sosPriorityRuleConfigRepository, cancellationToken);

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
                priorityThresholds));
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
        PriorityThresholdConfig priorityThresholds)
    {
        var minimumTargetScore = GetMinimumScoreForPriority(escalatedPriority, priorityThresholds);
        if (!currentScore.HasValue)
        {
            return minimumTargetScore > 0 ? minimumTargetScore : 1d;
        }

        return Math.Max(currentScore.Value + 1d, minimumTargetScore);
    }

    private static async Task<PriorityThresholdConfig> LoadPriorityThresholdsAsync(
        ISosPriorityRuleConfigRepository sosPriorityRuleConfigRepository,
        CancellationToken cancellationToken)
    {
        var config = await sosPriorityRuleConfigRepository.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(config?.PriorityThresholdsJson))
        {
            return new PriorityThresholdConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<PriorityThresholdConfig>(config.PriorityThresholdsJson) ?? new PriorityThresholdConfig();
        }
        catch
        {
            return new PriorityThresholdConfig();
        }
    }

    private static double GetMinimumScoreForPriority(SosPriorityLevel priorityLevel, PriorityThresholdConfig thresholds) =>
        priorityLevel switch
        {
            SosPriorityLevel.Critical => thresholds.Critical?.MinScore ?? 70d,
            SosPriorityLevel.High => thresholds.High?.MinScore ?? 45d,
            SosPriorityLevel.Medium => thresholds.Medium?.MinScore ?? 25d,
            _ => 0d
        };

    private class PriorityThresholdConfig
    {
        public ThresholdEntry? Critical { get; set; }
        public ThresholdEntry? High { get; set; }
        public ThresholdEntry? Medium { get; set; }
    }

    private class ThresholdEntry
    {
        public double MinScore { get; set; }
    }
}