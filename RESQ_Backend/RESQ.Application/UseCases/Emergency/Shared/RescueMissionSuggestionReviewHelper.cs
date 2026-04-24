using System.Globalization;
using System.Text;
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
        var nearbyTeamNameLookup = nearbyTeams
            .Where(team => !string.IsNullOrWhiteSpace(team.TeamName))
            .GroupBy(team => NormalizeLookupKey(team.TeamName))
            .ToDictionary(group => group.Key, group => group.First());

        if (nearbyTeamLookup.Count == 0)
        {
            warnings.Add("Không có đội Available nào nằm trong bán kính cluster hiện tại; điều phối viên cần gán đội thủ công.");
        }

        NormalizeExecutionMetadata(result.SuggestedActivities, warnings);

        var teamAssignmentErrors = new List<string>();

        result.SuggestedTeam = SanitizeSuggestedTeam(
            result.SuggestedTeam,
            nearbyTeamLookup,
            nearbyTeamNameLookup,
            "Đội tổng thể của mission",
            teamAssignmentErrors);

        var unassignedSteps = new List<int>();

        foreach (var activity in result.SuggestedActivities.OrderBy(activity => activity.Step))
        {
            activity.SuggestedTeam = SanitizeSuggestedTeam(
                activity.SuggestedTeam,
                nearbyTeamLookup,
                nearbyTeamNameLookup,
                $"Activity step {activity.Step} ({activity.ActivityType})",
                teamAssignmentErrors);

            if (activity.SuggestedTeam is null && nearbyTeamLookup.Count > 0)
            {
                unassignedSteps.Add(activity.Step);
            }
        }

        if (teamAssignmentErrors.Count > 0)
        {
            var uniqueErrors = teamAssignmentErrors.Distinct().ToList();
            if (uniqueErrors.Count > 3)
            {
                warnings.Add($"AI đã đề xuất đội không hợp lệ cho nhiều bước. Vui lòng kiểm tra lại cấu hình pool nearby teams.");
            }
            else
            {
                warnings.AddRange(uniqueErrors);
            }
        }

        if (unassignedSteps.Count > 0)
        {
            warnings.Add($"Các bước sau chưa được gán đội hợp lệ (vui lòng gán thủ công): {string.Join(", ", unassignedSteps)}.");
        }

        if (warnings.Count == 0)
            return;

        result.NeedsManualReview = true;
        result.SpecialNotes = AppendWarnings(result.SpecialNotes, warnings);
    }

    public static void ApplyNearbyDepotConstraints(
        RescueMissionSuggestionResult result,
        IReadOnlyCollection<DepotSummary> nearbyDepots)
    {
        var warnings = new List<string>();
        var nearbyDepotLookup = nearbyDepots
            .Where(depot => depot.Id > 0)
            .GroupBy(depot => depot.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var nearbyDepotNameLookup = nearbyDepots
            .Where(depot => !string.IsNullOrWhiteSpace(depot.Name))
            .GroupBy(depot => NormalizeLookupKey(depot.Name))
            .ToDictionary(group => group.Key, group => group.First());

        if (nearbyDepotLookup.Count == 0
            && result.SuggestedActivities.Any(RequiresDepotAssignment))
        {
            warnings.Add("Không có kho eligible nào trong pool nearby depots của cluster hiện tại; điều phối viên cần chọn kho thủ công.");
        }

        var depotAssignmentErrors = new List<string>();
        var unassignedDepotSteps = new List<int>();

        foreach (var activity in result.SuggestedActivities.OrderBy(activity => activity.Step))
        {
            if (!RequiresDepotAssignment(activity)
                && !activity.DepotId.HasValue
                && string.IsNullOrWhiteSpace(activity.DepotName))
            {
                continue;
            }

            var canonicalDepot = ResolveCanonicalDepot(
                activity.DepotId,
                activity.DepotName,
                nearbyDepotLookup,
                nearbyDepotNameLookup);

            if (canonicalDepot is null)
            {
                if (activity.DepotId.HasValue && activity.DepotId.Value > 0)
                {
                    depotAssignmentErrors.Add($"Bước {activity.Step} tham chiếu depot_id={activity.DepotId.Value} ngoài pool.");
                }
                else if (!string.IsNullOrWhiteSpace(activity.DepotName))
                {
                    depotAssignmentErrors.Add($"Bước {activity.Step} tham chiếu depot_name='{activity.DepotName!.Trim()}' ngoài pool.");
                }
                else if (RequiresDepotAssignment(activity))
                {
                    depotAssignmentErrors.Add($"Bước {activity.Step} thiếu depot hợp lệ.");
                }

                ClearActivityDepot(activity);

                if (RequiresDepotAssignment(activity) && nearbyDepotLookup.Count > 0)
                {
                    unassignedDepotSteps.Add(activity.Step);
                }

                continue;
            }

            activity.DepotId = canonicalDepot.Id;
            activity.DepotName = canonicalDepot.Name;
            activity.DepotAddress = string.IsNullOrWhiteSpace(canonicalDepot.Address)
                ? activity.DepotAddress?.Trim()
                : canonicalDepot.Address.Trim();
        }

        foreach (var shortage in result.SupplyShortages)
        {
            var canonicalDepot = ResolveCanonicalDepot(
                shortage.SelectedDepotId,
                shortage.SelectedDepotName,
                nearbyDepotLookup,
                nearbyDepotNameLookup);

            if (canonicalDepot is null)
            {
                if (shortage.SelectedDepotId.HasValue && shortage.SelectedDepotId.Value > 0)
                {
                    depotAssignmentErrors.Add($"Supply shortage '{shortage.ItemName}' tham chiếu depot_id={shortage.SelectedDepotId.Value} ngoài pool.");
                }

                shortage.SelectedDepotId = null;
                shortage.SelectedDepotName = null;
                continue;
            }

            shortage.SelectedDepotId = canonicalDepot.Id;
            shortage.SelectedDepotName = canonicalDepot.Name;
        }

        if (depotAssignmentErrors.Count > 0)
        {
            var uniqueErrors = depotAssignmentErrors.Distinct().ToList();
            if (uniqueErrors.Count > 3)
            {
                warnings.Add($"Nhiều bước tham chiếu kho không hợp lệ.");
            }
            else
            {
                warnings.AddRange(uniqueErrors);
            }
        }

        if (unassignedDepotSteps.Count > 0)
        {
            warnings.Add($"Các bước sau cần chọn kho thủ công: {string.Join(", ", unassignedDepotSteps)}.");
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
        IReadOnlyDictionary<string, AgentTeamInfo> nearbyTeamNameLookup,
        string contextLabel,
        ICollection<string> warnings)
    {
        if (suggestedTeam is null)
            return null;

        var canonicalTeam = ResolveCanonicalTeam(suggestedTeam, nearbyTeamLookup, nearbyTeamNameLookup);
        if (canonicalTeam is null)
        {
            if (suggestedTeam.TeamId > 0)
            {
                warnings.Add($"{contextLabel} tham chiếu team_id={suggestedTeam.TeamId} ngoài pool.");
            }
            else
            {
                warnings.Add($"{contextLabel} thiếu team_id hợp lệ.");
            }

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

    private static AgentTeamInfo? ResolveCanonicalTeam(
        SuggestedTeamDto suggestedTeam,
        IReadOnlyDictionary<int, AgentTeamInfo> nearbyTeamLookup,
        IReadOnlyDictionary<string, AgentTeamInfo> nearbyTeamNameLookup)
    {
        if (suggestedTeam.TeamId > 0
            && nearbyTeamLookup.TryGetValue(suggestedTeam.TeamId, out var canonicalById))
        {
            return canonicalById;
        }

        if (!string.IsNullOrWhiteSpace(suggestedTeam.TeamName))
        {
            var normalizedName = NormalizeLookupKey(suggestedTeam.TeamName);
            if (nearbyTeamNameLookup.TryGetValue(normalizedName, out var canonicalByName))
                return canonicalByName;
        }

        return null;
    }

    private static DepotSummary? ResolveCanonicalDepot(
        int? depotId,
        string? depotName,
        IReadOnlyDictionary<int, DepotSummary> nearbyDepotLookup,
        IReadOnlyDictionary<string, DepotSummary> nearbyDepotNameLookup)
    {
        if (depotId is > 0
            && nearbyDepotLookup.TryGetValue(depotId.Value, out var canonicalById))
        {
            return canonicalById;
        }

        if (!string.IsNullOrWhiteSpace(depotName))
        {
            var normalizedName = NormalizeLookupKey(depotName);
            if (nearbyDepotNameLookup.TryGetValue(normalizedName, out var canonicalByName))
                return canonicalByName;
        }

        return null;
    }

    private static bool RequiresDepotAssignment(SuggestedActivityDto activity) =>
        activity.ActivityType is not null
        && (activity.ActivityType.Equals("COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
            || activity.ActivityType.Equals("DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase)
            || activity.ActivityType.Equals("RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase));

    private static void ClearActivityDepot(SuggestedActivityDto activity)
    {
        var previousDepotName = activity.DepotName?.Trim();

        activity.DepotId = null;
        activity.DepotName = null;
        activity.DepotAddress = null;

        if (activity.ActivityType is not null
            && (activity.ActivityType.Equals("COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                || activity.ActivityType.Equals("RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase)))
        {
            if (!string.IsNullOrWhiteSpace(previousDepotName)
                && string.Equals(activity.DestinationName?.Trim(), previousDepotName, StringComparison.OrdinalIgnoreCase))
            {
                activity.DestinationName = null;
            }

            activity.DestinationLatitude = null;
            activity.DestinationLongitude = null;
        }
    }

    private static string NormalizeLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousWasWhitespace = false;

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(char.ToUpperInvariant(ch));
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
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
