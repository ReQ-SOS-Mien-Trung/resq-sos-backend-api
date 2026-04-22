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
            warnings.Add("Không có đội Available nào nằm trong bán kính cluster hiện tại; điều phối viên cần gán đội thủ công.");
        }

        NormalizeExecutionMetadata(result.SuggestedActivities, warnings);

        result.SuggestedTeam = SanitizeSuggestedTeam(
            result.SuggestedTeam,
            nearbyTeamLookup,
            "Đội tổng thể của mission suggestion",
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
                warnings.Add($"Activity step {activity.Step} ({activity.ActivityType}) chưa được gán đội trong pool nearby teams.");
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
            var normalizedExecutionMode = NormalizeExecutionMode(
                activity.ExecutionMode,
                activity.RequiredTeamCount,
                activity.CoordinationGroupKey);

            if (string.Equals(normalizedExecutionMode, SplitAcrossTeamsMode, StringComparison.OrdinalIgnoreCase)
                || activity.RequiredTeamCount.GetValueOrDefault() > 1)
            {
                warnings.Add(
                    $"Activity step {activity.Step} ({activity.ActivityType}) đã được backend chuẩn hóa về SingleTeam theo business rule.");
            }

            activity.ExecutionMode = SingleTeamMode;
            activity.RequiredTeamCount = 1;
            activity.CoordinationGroupKey = string.IsNullOrWhiteSpace(activity.CoordinationGroupKey)
                ? null
                : activity.CoordinationGroupKey.Trim();
            activity.CoordinationNotes = string.IsNullOrWhiteSpace(activity.CoordinationNotes)
                ? "Một đội có thể hoàn thành activity này độc lập."
                : activity.CoordinationNotes.Trim();
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
            warnings.Add($"{contextLabel} thiếu team_id hợp lệ.");
            return null;
        }

        if (!nearbyTeamLookup.TryGetValue(suggestedTeam.TeamId, out var canonicalTeam))
        {
            warnings.Add($"{contextLabel} đang tham chiếu team_id={suggestedTeam.TeamId} nằm ngoài pool nearby teams.");
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
            return $"Activity này là một phần của kế hoạch nhiều đội tại depot {(activity.DepotName ?? $"#{activity.DepotId}")}.";
        }

        if (activity.SosRequestId.HasValue)
        {
            return $"Activity này là một nhánh trong kế hoạch nhiều đội để xử lý SOS #{activity.SosRequestId}.";
        }

        return "Activity này là một phần của kế hoạch nhiều đội và cần điều phối cùng các activity khác trong cùng coordination_group_key.";
    }

    private static string BuildSameDepotSplitNotes(SuggestedActivityDto activity, int teamCount)
    {
        var depotLabel = activity.DepotName ?? $"#{activity.DepotId}";
        var teamLabel = activity.SuggestedTeam?.TeamName ?? $"team #{activity.SuggestedTeam?.TeamId}";
        return $"Kho {depotLabel} đang được chia cho {teamCount} đội; activity này là phần lấy vật phẩm dành cho {teamLabel}.";
    }

    private static string BuildDefaultTeamReason(AgentTeamInfo team)
    {
        return team.DistanceKm.HasValue
            ? $"Đội nằm trong pool nearby teams của cluster, cách tâm cluster khoảng {team.DistanceKm.Value:0.##} km."
            : "Đội nằm trong pool nearby teams của cluster hiện tại.";
    }

    private static string AppendWarnings(string? existingNotes, IEnumerable<string> warnings)
    {
        var distinctWarnings = warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinctWarnings.Count == 0)
            return existingNotes ?? string.Empty;

        var reviewSection = "[CẦN REVIEW THỦ CÔNG] " + string.Join(" | ", distinctWarnings);
        if (string.IsNullOrWhiteSpace(existingNotes))
            return reviewSection;

        return existingNotes.TrimEnd() + Environment.NewLine + reviewSection;
    }
}
