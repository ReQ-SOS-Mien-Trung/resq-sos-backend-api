using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Entities.Personnel.Exceptions;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Tests.Application.UseCases.Workflows;

/// <summary>
/// Luồng 6 – Không có đội cứu hộ khả dụng: Team không ở Available → Không thể assign mission.
/// Validates RescueTeamModel state constraints that prevent mission assignment.
/// </summary>
public class Flow6_NoRescuerAvailableTests
{
    private static readonly Guid CoordinatorId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Guid LeaderId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

    // ────────── Only Available teams can be assigned ──────────

    [Fact]
    public void RescueTeam_Available_CanAssignMission()
    {
        var team = CreateAvailableTeam();

        team.AssignMission();

        Assert.Equal(RescueTeamStatus.Assigned, team.Status);
    }

    [Fact]
    public void RescueTeam_Gathering_CannotAssignMission()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, CoordinatorId, 6);
        Assert.Equal(RescueTeamStatus.Gathering, team.Status);

        Assert.Throws<InvalidTeamTransitionException>(() => team.AssignMission());
    }

    [Fact]
    public void RescueTeam_Assigned_CannotAssignMissionAgain()
    {
        var team = CreateAvailableTeam();
        team.AssignMission();
        Assert.Equal(RescueTeamStatus.Assigned, team.Status);

        Assert.Throws<InvalidTeamTransitionException>(() => team.AssignMission());
    }

    [Fact]
    public void RescueTeam_OnMission_CannotAssignAnotherMission()
    {
        var team = CreateAvailableTeam();
        team.AssignMission();
        team.StartMission();
        Assert.Equal(RescueTeamStatus.OnMission, team.Status);

        Assert.Throws<InvalidTeamTransitionException>(() => team.AssignMission());
    }

    [Fact]
    public void RescueTeam_Stuck_CannotAssignMission()
    {
        var team = CreateAvailableTeam();
        team.AssignMission();
        team.StartMission();
        team.ReportIncident();
        Assert.Equal(RescueTeamStatus.Stuck, team.Status);

        Assert.Throws<InvalidTeamTransitionException>(() => team.AssignMission());
    }

    [Fact]
    public void RescueTeam_Unavailable_CannotAssignMission()
    {
        var team = CreateAvailableTeam();
        team.SetUnavailable();
        Assert.Equal(RescueTeamStatus.Unavailable, team.Status);

        Assert.Throws<InvalidTeamTransitionException>(() => team.AssignMission());
    }

    [Fact]
    public void RescueTeam_Disbanded_CannotAssignMission()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, CoordinatorId, 6);
        team.AddMember(LeaderId, true, "Core", null);
        team.Disband(); // Gathering → Disbanded
        Assert.Equal(RescueTeamStatus.Disbanded, team.Status);

        Assert.Throws<InvalidTeamTransitionException>(() => team.AssignMission());
    }

    // ────────── Team becomes Available again after finishing mission ──────────

    [Fact]
    public void RescueTeam_AfterFinishMission_CanBeAssignedAgain()
    {
        var team = CreateAvailableTeam();
        team.AssignMission();
        team.StartMission();
        team.FinishMission();
        Assert.Equal(RescueTeamStatus.Available, team.Status);

        // Can now accept a new mission
        team.AssignMission();
        Assert.Equal(RescueTeamStatus.Assigned, team.Status);
    }

    [Fact]
    public void RescueTeam_AfterCancelMission_CanBeAssignedAgain()
    {
        var team = CreateAvailableTeam();
        team.AssignMission();
        Assert.Equal(RescueTeamStatus.Assigned, team.Status);

        team.CancelMission();
        Assert.Equal(RescueTeamStatus.Available, team.Status);

        team.AssignMission();
        Assert.Equal(RescueTeamStatus.Assigned, team.Status);
    }

    [Fact]
    public void RescueTeam_AfterIncidentResolvedNoInjury_CanBeAssignedAgain()
    {
        var team = CreateAvailableTeam();
        team.AssignMission();
        team.StartMission();
        team.ReportIncident();
        team.ResolveIncident(hasInjuredMember: false);
        Assert.Equal(RescueTeamStatus.Available, team.Status);

        team.AssignMission();
        Assert.Equal(RescueTeamStatus.Assigned, team.Status);
    }

    [Fact]
    public void RescueTeam_AfterIncidentResolvedWithInjury_CannotBeAssigned()
    {
        var team = CreateAvailableTeam();
        team.AssignMission();
        team.StartMission();
        team.ReportIncident();
        team.ResolveIncident(hasInjuredMember: true);
        Assert.Equal(RescueTeamStatus.Unavailable, team.Status);

        Assert.Throws<InvalidTeamTransitionException>(() => team.AssignMission());
    }

    // ────────── Multiple teams: filter by status ──────────

    [Fact]
    public void MultipleTeams_OnlyAvailableCanBeAssigned()
    {
        var teamA = CreateAvailableTeam("Alpha");
        var teamB = RescueTeamModel.Create("Bravo", RescueTeamType.Rescue, 1, CoordinatorId, 6);
        // teamB is Gathering → not available
        var teamC = CreateAvailableTeam("Charlie");
        teamC.AssignMission();
        teamC.StartMission();
        // teamC is OnMission → not available

        var allTeams = new[] { teamA, teamB, teamC };
        var availableTeams = allTeams.Where(t => t.Status == RescueTeamStatus.Available).ToList();

        Assert.Single(availableTeams);
        Assert.Equal("Alpha", availableTeams[0].Name);
    }

    // ────────── Helper ──────────

    private static RescueTeamModel CreateAvailableTeam(string name = "Alpha")
    {
        var team = RescueTeamModel.Create(name, RescueTeamType.Rescue, 1, CoordinatorId, 6);
        team.AddMember(LeaderId, true, "Core", null);
        team.SetAvailableByLeader(LeaderId);
        return team;
    }
}
