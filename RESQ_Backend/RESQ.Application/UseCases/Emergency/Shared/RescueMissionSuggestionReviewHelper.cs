using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Emergency.Shared;

public static class RescueMissionSuggestionReviewHelper
{
    private const string SingleTeamMode = "SingleTeam";
    private const string SplitAcrossTeamsMode = "SplitAcrossTeams";

    public static void ApplyNearbyTeamConstraints(
        RescueMissionSuggestionResult result,
        IReadOnlyCollection<AgentTeamInfo> nearbyTeams)
    {
        var warnings = new List<string>();
        var nearbyTeamLookup = nearbyTeams
            .Where(team => team.TeamId > 0)
            .GroupBy(team => team.TeamId)
            .ToDictionary(group => group.Key, group => group.First());

        if (nearbyTeamLookup.Count == 0)
        {
            warnings.Add("Không có d?i Available nào n?m trong bán kính cluster hi?n t?i; di?u ph?i viên c?n gán d?i th? công.");
        }

        NormalizeExecutionMetadata(result.SuggestedActivities, warnings);

        result.SuggestedTeam = SanitizeSuggestedTeam(
            result.SuggestedTeam,
            nearbyTeamLookup,
            "Ð?i t?ng th? c?a mission suggestion",
            warnings);

        foreach (var activity in result.SuggestedActivities.OrderBy(activity => activity.Step))
        {
            activity.SuggestedTeam = SanitizeSuggestedTeam(
                activity.SuggestedTeam,
                nearbyTeamLookup,
                $"Activity step {activity.Step} ({activity.ActivityType})",
                warnings);

            if (activity.SuggestedTeam is null && nearbyTeamLookup.Count > 0)
            {
                warnings.Add($"Activity step {activity.Step} ({activity.ActivityType}) chua du?c gán d?i trong pool nearby teams.");
            }
        }

        if (warnings.Count == 0)
            return;

        result.NeedsManualReview = true;
        result.SpecialNotes = AppendWarnings(result.SpecialNotes, warnings);
    }

    private static void NormalizeExecutionMetadata(
        IReadOnlyCollection<SuggestedActivityDto> activities,
        ICollection<string> warnings)
    {
        foreach (var activity in activities)
        {
            activity.ExecutionMode = NormalizeExecutionMode(
                activity.ExecutionMode,
                activity.RequiredTeamCount,
                activity.CoordinationGroupKey);

            if (string.Equals(activity.ExecutionMode, SplitAcrossTeamsMode, StringComparison.OrdinalIgnoreCase))
            {
                activity.CoordinationGroupKey = string.IsNullOrWhiteSpace(activity.CoordinationGroupKey)
                    ? BuildFallbackCoordinationGroupKey(activity)
                    : activity.CoordinationGroupKey.Trim();
                activity.CoordinationNotes = string.IsNullOrWhiteSpace(activity.CoordinationNotes)
                    ? BuildDefaultSplitNotes(activity)
                    : activity.CoordinationNotes.Trim();
            }
            else
            {
                activity.ExecutionMode = SingleTeamMode;
                activity.RequiredTeamCount = 1;
                activity.CoordinationGroupKey = null;
                activity.CoordinationNotes = string.IsNullOrWhiteSpace(activity.CoordinationNotes)
                    ? "M?t d?i có th? hoàn thành activity này d?c l?p."
                    : activity.CoordinationNotes.Trim();
            }
        }

        InferSameDepotSplitActivities(activities, warnings);

        var splitGroups = activities
            .Where(activity => string.Equals(activity.ExecutionMode, SplitAcrossTeamsMode, StringComparison.OrdinalIgnoreCase))
            .GroupBy(activity => activity.CoordinationGroupKey ?? BuildFallbackCoordinationGroupKey(activity))
            .ToList();

        foreach (var group in splitGroups)
        {
            var assignedTeamCount = group
                .Where(activity => activity.SuggestedTeam is not null)
                .Select(activity => activity.SuggestedTeam!.TeamId)
                .Distinct()
                .Count();

            var requiredTeamCount = Math.Max(
                Math.Max(group.Max(activity => activity.RequiredTeamCount ?? 0), assignedTeamCount),
                2);

            foreach (var activity in group)
            {
                activity.ExecutionMode = SplitAcrossTeamsMode;
                activity.CoordinationGroupKey = group.Key;
                activity.RequiredTeamCount = requiredTeamCount;
                activity.CoordinationNotes = string.IsNullOrWhiteSpace(activity.CoordinationNotes)
                    ? BuildDefaultSplitNotes(activity)
                    : activity.CoordinationNotes.Trim();
            }

            if (assignedTeamCount <= 1)
            {
                warnings.Add(
                    $"Nhóm ph?i h?p '{group.Key}' dang du?c dánh d?u SplitAcrossTeams nhung m?i có {assignedTeamCount} d?i du?c gán.");
            }
        }
    }

    private static void InferSameDepotSplitActivities(
        IReadOnlyCollection<SuggestedActivityDto> activities,
        ICollection<string> warnings)
    {
        var depotSplitGroups = activities
            .Where(activity => activity.DepotId.HasValue
                && string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                && activity.SuggestedTeam is not null)
            .GroupBy(activity => new { activity.DepotId, activity.SosRequestId })
            .Where(group => group
                .Select(activity => activity.SuggestedTeam!.TeamId)
                .Distinct()
                .Count() > 1)
            .ToList();

        foreach (var group in depotSplitGroups)
        {
            var distinctTeamCount = group
                .Select(activity => activity.SuggestedTeam!.TeamId)
                .Distinct()
                .Count();

            var groupKey = group
                .Select(activity => activity.CoordinationGroupKey)
                .FirstOrDefault(key => !string.IsNullOrWhiteSpace(key))
                ?? $"collect-depot-{group.Key.DepotId}-sos-{group.Key.SosRequestId ?? 0}";

            foreach (var activity in group)
            {
                if (!string.Equals(activity.ExecutionMode, SplitAcrossTeamsMode, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(
                        $"Activity step {activity.Step} du?c backend suy di?n là SplitAcrossTeams vì có nhi?u d?i cùng l?y v?t ph?m t?i depot #{group.Key.DepotId}.");
                }

                activity.ExecutionMode = SplitAcrossTeamsMode;
                activity.CoordinationGroupKey = groupKey;
                activity.RequiredTeamCount = Math.Max(activity.RequiredTeamCount ?? 0, distinctTeamCount);
                activity.CoordinationNotes = BuildSameDepotSplitNotes(activity, distinctTeamCount);
            }
        }
    }

    private static SuggestedTeamDto? SanitizeSuggestedTeam(
        SuggestedTeamDto? suggestedTeam,
        IReadOnlyDictionary<int, AgentTeamInfo> nearbyTeamLookup,
        string contextLabel,
        ICollection<string> warnings)
    {
        if (suggestedTeam is null)
            return null;

        if (suggestedTeam.TeamId <= 0)
        {
            warnings.Add($"{contextLabel} thi?u team_id h?p l?.");
            return null;
        }

        if (!nearbyTeamLookup.TryGetValue(suggestedTeam.TeamId, out var canonicalTeam))
        {
            warnings.Add($"{contextLabel} dang tham chi?u team_id={suggestedTeam.TeamId} n?m ngoài pool nearby teams.");
            return null;
        }

        return new SuggestedTeamDto
        {
            TeamId = canonicalTeam.TeamId,
            TeamName = canonicalTeam.TeamName,
            TeamType = canonicalTeam.TeamType,
            Reason = string.IsNullOrWhiteSpace(suggestedTeam.Reason)
                ? BuildDefaultTeamReason(canonicalTeam)
                : suggestedTeam.Reason!.Trim(),
            AssemblyPointId = canonicalTeam.AssemblyPointId,
            AssemblyPointName = canonicalTeam.AssemblyPointName,
            Latitude = canonicalTeam.Latitude,
            Longitude = canonicalTeam.Longitude,
            DistanceKm = canonicalTeam.DistanceKm
        };
    }

    private static string NormalizeExecutionMode(
        string? executionMode,
        int? requiredTeamCount,
        string? coordinationGroupKey)
    {
        if (string.IsNullOrWhiteSpace(executionMode))
        {
            return requiredTeamCount.GetValueOrDefault() > 1 || !string.IsNullOrWhiteSpace(coordinationGroupKey)
                ? SplitAcrossTeamsMode
                : SingleTeamMode;
        }

        var normalized = executionMode.Trim().Replace("_", string.Empty).Replace("-", string.Empty);
        if (normalized.Equals(SingleTeamMode, StringComparison.OrdinalIgnoreCase))
            return SingleTeamMode;
        if (normalized.Equals("SplitTeams", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("SplitAcrossTeams", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("MultiTeam", StringComparison.OrdinalIgnoreCase))
        {
            return SplitAcrossTeamsMode;
        }

        return requiredTeamCount.GetValueOrDefault() > 1 || !string.IsNullOrWhiteSpace(coordinationGroupKey)
            ? SplitAcrossTeamsMode
            : SingleTeamMode;
    }

    private static string BuildFallbackCoordinationGroupKey(SuggestedActivityDto activity)
    {
        var activityType = string.IsNullOrWhiteSpace(activity.ActivityType)
            ? "activity"
            : activity.ActivityType.Trim().ToLowerInvariant();

        return $"{activityType}-sos-{activity.SosRequestId ?? 0}-depot-{activity.DepotId ?? 0}-assembly-{activity.AssemblyPointId ?? 0}";
    }

    private static string BuildDefaultSplitNotes(SuggestedActivityDto activity)
    {
        if (activity.DepotId.HasValue)
        {
            return $"Activity này là m?t ph?n c?a k? ho?ch nhi?u d?i t?i depot {(activity.DepotName ?? $"#{activity.DepotId}")}.";
        }

        if (activity.SosRequestId.HasValue)
        {
            return $"Activity này là m?t nhánh trong k? ho?ch nhi?u d?i d? x? lý SOS #{activity.SosRequestId}.";
        }

        return "Activity này là m?t ph?n c?a k? ho?ch nhi?u d?i và c?n di?u ph?i cùng các activity khác trong cùng coordination_group_key.";
    }

    private static string BuildSameDepotSplitNotes(SuggestedActivityDto activity, int teamCount)
    {
        var depotLabel = activity.DepotName ?? $"#{activity.DepotId}";
        var teamLabel = activity.SuggestedTeam?.TeamName ?? $"team #{activity.SuggestedTeam?.TeamId}";
        return $"Kho {depotLabel} dang du?c chia cho {teamCount} d?i; activity này là ph?n l?y v?t ph?m dành cho {teamLabel}.";
    }

    private static string BuildDefaultTeamReason(AgentTeamInfo team)
    {
        return team.DistanceKm.HasValue
            ? $"Ð?i n?m trong pool nearby teams c?a cluster, cách tâm cluster kho?ng {team.DistanceKm.Value:0.##} km."
            : "Ð?i n?m trong pool nearby teams c?a cluster hi?n t?i.";
    }

    private static string AppendWarnings(string? existingNotes, IEnumerable<string> warnings)
    {
        var distinctWarnings = warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinctWarnings.Count == 0)
            return existingNotes ?? string.Empty;

        var reviewSection = "[C?N REVIEW TH? CÔNG] " + string.Join(" | ", distinctWarnings);
        if (string.IsNullOrWhiteSpace(existingNotes))
            return reviewSection;

        return existingNotes.TrimEnd() + Environment.NewLine + reviewSection;
    }
}
