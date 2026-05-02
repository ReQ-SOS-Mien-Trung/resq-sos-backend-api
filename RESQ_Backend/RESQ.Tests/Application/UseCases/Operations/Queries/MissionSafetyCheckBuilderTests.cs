using RESQ.Application.UseCases.Operations.Queries.GetMissions;

namespace RESQ.Tests.Application.UseCases.Operations.Queries;

public class MissionSafetyCheckBuilderTests
{
    private static readonly DateTime Now = new(2026, 5, 2, 8, 45, 0, DateTimeKind.Utc);

    [Fact]
    public void Apply_ReturnsSafeSummary_WhenAllTeamsAreSafe()
    {
        var mission = new MissionDto
        {
            Status = "OnGoing",
            Teams =
            [
                CreateTeam(1, "Safe", Now.AddMinutes(-15), Now.AddMinutes(90)),
                CreateTeam(2, "Safe", Now.AddMinutes(-5), Now.AddMinutes(30))
            ]
        };

        MissionSafetyCheckBuilder.Apply(mission, Now);

        Assert.NotNull(mission.SafetyCheck);
        Assert.Equal("Safe", mission.SafetyCheck!.OverallStatus);
        Assert.True(mission.SafetyCheck.IsMonitoringActive);
        Assert.Equal(2, mission.SafetyCheck.TotalTeams);
        Assert.Equal(2, mission.SafetyCheck.SafeTeams);
        Assert.Equal(0, mission.SafetyCheck.OverdueTeams);
        Assert.Equal(Now.AddMinutes(30), mission.SafetyCheck.NextTimeoutAt);
        Assert.Equal(Now.AddMinutes(-5), mission.SafetyCheck.LatestCheckInAt);
        Assert.Equal(Now, mission.SafetyCheck.ServerNowUtc);
        Assert.All(mission.Teams, team => Assert.NotNull(team.SafetyCheck));
    }

    [Fact]
    public void Apply_ReturnsAtRisk_WhenSafeTeamIsOverdue()
    {
        var mission = new MissionDto
        {
            Status = "OnGoing",
            Teams = [CreateTeam(1, "Safe", Now.AddMinutes(-90), Now.AddMinutes(-1))]
        };

        MissionSafetyCheckBuilder.Apply(mission, Now);

        Assert.Equal("AtRisk", mission.SafetyCheck!.OverallStatus);
        Assert.Equal(1, mission.SafetyCheck.OverdueTeams);
        Assert.True(mission.Teams[0].SafetyCheck!.IsOverdue);
    }

    [Fact]
    public void Apply_ReturnsAtRisk_WhenAnyTeamIsAtRisk()
    {
        var mission = new MissionDto
        {
            Status = "OnGoing",
            Teams = [CreateTeam(1, "AtRisk", Now.AddMinutes(-30), Now.AddMinutes(20))]
        };

        MissionSafetyCheckBuilder.Apply(mission, Now);

        Assert.Equal("AtRisk", mission.SafetyCheck!.OverallStatus);
        Assert.Equal(1, mission.SafetyCheck.AtRiskTeams);
        Assert.Equal(0, mission.SafetyCheck.OverdueTeams);
    }

    [Fact]
    public void Apply_ReturnsSosCreated_WithHighestPriority()
    {
        var mission = new MissionDto
        {
            Status = "OnGoing",
            Teams =
            [
                CreateTeam(1, "AtRisk", Now.AddMinutes(-30), Now.AddMinutes(-5)),
                CreateTeam(2, "SosCreated", Now.AddMinutes(-40), Now.AddMinutes(-10), generatedSosRequestId: 77)
            ]
        };

        MissionSafetyCheckBuilder.Apply(mission, Now);

        Assert.Equal("SosCreated", mission.SafetyCheck!.OverallStatus);
        Assert.Equal(1, mission.SafetyCheck.SosCreatedTeams);
        Assert.Equal(77, mission.Teams[1].SafetyCheck!.GeneratedSosRequestId);
    }

    [Fact]
    public void Apply_ReturnsInactive_WhenMissionIsNotOngoing()
    {
        var mission = new MissionDto
        {
            Status = "Completed",
            Teams = [CreateTeam(1, "Safe", Now.AddMinutes(-30), Now.AddMinutes(20))]
        };

        MissionSafetyCheckBuilder.Apply(mission, Now);

        Assert.Equal("Inactive", mission.SafetyCheck!.OverallStatus);
        Assert.False(mission.SafetyCheck.IsMonitoringActive);
        Assert.False(mission.Teams[0].SafetyCheck!.IsMonitoringActive);
    }

    [Fact]
    public void Apply_ReturnsInactive_WhenAnyTeamIsInactive()
    {
        var mission = new MissionDto
        {
            Status = "OnGoing",
            Teams = [CreateTeam(1, "Inactive", Now.AddMinutes(-30), null)]
        };

        MissionSafetyCheckBuilder.Apply(mission, Now);

        Assert.Equal("Inactive", mission.SafetyCheck!.OverallStatus);
        Assert.Equal(1, mission.SafetyCheck.InactiveTeams);
    }

    [Fact]
    public void Apply_ReturnsUnknown_WhenOngoingMissionHasNoTeams()
    {
        var mission = new MissionDto { Status = "OnGoing" };

        MissionSafetyCheckBuilder.Apply(mission, Now);

        Assert.Equal("Unknown", mission.SafetyCheck!.OverallStatus);
        Assert.False(mission.SafetyCheck.IsMonitoringActive);
        Assert.Equal(0, mission.SafetyCheck.TotalTeams);
    }

    [Fact]
    public void Apply_ReturnsUnknown_WhenTeamStatusIsMissing()
    {
        var mission = new MissionDto
        {
            Status = "OnGoing",
            Teams = [CreateTeam(1, null, Now.AddMinutes(-30), Now.AddMinutes(20))]
        };

        MissionSafetyCheckBuilder.Apply(mission, Now);

        Assert.Equal("Unknown", mission.SafetyCheck!.OverallStatus);
        Assert.Equal(1, mission.SafetyCheck.UnknownTeams);
        Assert.Equal("Unknown", mission.Teams[0].SafetyCheck!.Status);
    }

    private static AssignedTeamDto CreateTeam(
        int missionTeamId,
        string? safetyStatus,
        DateTime? latestCheckInAt,
        DateTime? timeoutAt,
        int? generatedSosRequestId = null)
    {
        return new AssignedTeamDto
        {
            MissionTeamId = missionTeamId,
            RescueTeamId = missionTeamId + 100,
            SafetyStatus = safetyStatus,
            SafetyLatestCheckInAt = latestCheckInAt,
            SafetyTimeoutAt = timeoutAt,
            GeneratedSosRequestId = generatedSosRequestId
        };
    }
}
