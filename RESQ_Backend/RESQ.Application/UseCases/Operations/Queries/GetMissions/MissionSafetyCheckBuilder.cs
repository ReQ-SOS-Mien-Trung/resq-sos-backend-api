namespace RESQ.Application.UseCases.Operations.Queries.GetMissions;

internal static class MissionSafetyCheckBuilder
{
    private const string SafeStatus = "Safe";
    private const string AtRiskStatus = "AtRisk";
    private const string SosCreatedStatus = "SosCreated";
    private const string InactiveStatus = "Inactive";
    private const string UnknownStatus = "Unknown";
    private const string OnGoingMissionStatus = "OnGoing";

    internal static void Apply(MissionDto mission, DateTime? serverNowUtc = null)
    {
        var now = NormalizeUtc(serverNowUtc ?? DateTime.UtcNow);
        var isMissionOngoing = IsOnGoing(mission.Status);

        foreach (var team in mission.Teams)
        {
            team.SafetyCheck = BuildTeamSafetyCheck(team, isMissionOngoing, now);
        }

        mission.SafetyCheck = BuildMissionSafetyCheck(
            mission.Status,
            mission.Teams.Select(team => team.SafetyCheck!).ToList(),
            now);
    }

    internal static MissionSafetyCheckDto BuildMissionSafetyCheck(
        string? missionStatus,
        IReadOnlyCollection<TeamSafetyCheckDto> teamChecks,
        DateTime? serverNowUtc = null)
    {
        var now = NormalizeUtc(serverNowUtc ?? DateTime.UtcNow);
        var isMissionOngoing = IsOnGoing(missionStatus);
        var totalTeams = teamChecks.Count;

        var safeTeams = teamChecks.Count(team => IsStatus(team.Status, SafeStatus));
        var atRiskTeams = teamChecks.Count(team => IsStatus(team.Status, AtRiskStatus));
        var sosCreatedTeams = teamChecks.Count(team => IsStatus(team.Status, SosCreatedStatus));
        var inactiveTeams = teamChecks.Count(team => IsStatus(team.Status, InactiveStatus));
        var unknownTeams = teamChecks.Count(team => IsStatus(team.Status, UnknownStatus));
        var overdueTeams = teamChecks.Count(team => team.IsOverdue);

        var overallStatus = ResolveOverallStatus(
            missionStatus,
            isMissionOngoing,
            totalTeams,
            atRiskTeams,
            sosCreatedTeams,
            inactiveTeams,
            unknownTeams,
            overdueTeams);

        return new MissionSafetyCheckDto
        {
            OverallStatus = overallStatus,
            IsMonitoringActive = isMissionOngoing && teamChecks.Any(team => team.IsMonitoringActive),
            TotalTeams = totalTeams,
            SafeTeams = safeTeams,
            AtRiskTeams = atRiskTeams,
            SosCreatedTeams = sosCreatedTeams,
            InactiveTeams = inactiveTeams,
            UnknownTeams = unknownTeams,
            OverdueTeams = overdueTeams,
            NextTimeoutAt = FirstOrNull(teamChecks
                .Where(team => team.IsMonitoringActive && team.TimeoutAt.HasValue)
                .Select(team => team.TimeoutAt!.Value)
                .OrderBy(timeout => timeout)),
            LatestCheckInAt = FirstOrNull(teamChecks
                .Where(team => team.LatestCheckInAt.HasValue)
                .Select(team => team.LatestCheckInAt!.Value)
                .OrderByDescending(checkIn => checkIn)),
            ServerNowUtc = now
        };
    }

    private static TeamSafetyCheckDto BuildTeamSafetyCheck(
        AssignedTeamDto team,
        bool isMissionOngoing,
        DateTime serverNowUtc)
    {
        var status = NormalizeTeamStatus(team.SafetyStatus, isMissionOngoing);
        var isMonitoringActive = isMissionOngoing && !IsStatus(status, InactiveStatus) && !IsStatus(status, UnknownStatus);
        var isOverdue = isMonitoringActive
            && !IsStatus(status, SosCreatedStatus)
            && team.SafetyTimeoutAt.HasValue
            && team.SafetyTimeoutAt.Value <= serverNowUtc;

        return new TeamSafetyCheckDto
        {
            MissionTeamId = team.MissionTeamId,
            RescueTeamId = team.RescueTeamId,
            Status = status,
            IsMonitoringActive = isMonitoringActive,
            LatestCheckInAt = team.SafetyLatestCheckInAt,
            TimeoutAt = team.SafetyTimeoutAt,
            IsOverdue = isOverdue,
            GeneratedSosRequestId = team.GeneratedSosRequestId
        };
    }

    private static string ResolveOverallStatus(
        string? missionStatus,
        bool isMissionOngoing,
        int totalTeams,
        int atRiskTeams,
        int sosCreatedTeams,
        int inactiveTeams,
        int unknownTeams,
        int overdueTeams)
    {
        if (sosCreatedTeams > 0)
            return SosCreatedStatus;

        if (atRiskTeams > 0 || overdueTeams > 0)
            return AtRiskStatus;

        if (string.IsNullOrWhiteSpace(missionStatus))
            return UnknownStatus;

        if (!isMissionOngoing || inactiveTeams > 0)
            return InactiveStatus;

        if (totalTeams == 0 || unknownTeams > 0)
            return UnknownStatus;

        return SafeStatus;
    }

    private static string NormalizeTeamStatus(string? status, bool isMissionOngoing)
    {
        if (string.IsNullOrWhiteSpace(status))
            return isMissionOngoing ? UnknownStatus : InactiveStatus;

        if (IsStatus(status, SafeStatus))
            return SafeStatus;

        if (IsStatus(status, AtRiskStatus))
            return AtRiskStatus;

        if (IsStatus(status, SosCreatedStatus))
            return SosCreatedStatus;

        if (IsStatus(status, InactiveStatus))
            return InactiveStatus;

        return UnknownStatus;
    }

    private static bool IsOnGoing(string? missionStatus) =>
        IsStatus(missionStatus, OnGoingMissionStatus);

    private static bool IsStatus(string? actual, string expected) =>
        string.Equals(actual?.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;

    private static DateTime? FirstOrNull(IEnumerable<DateTime> values)
    {
        foreach (var value in values)
            return value;

        return null;
    }
}
