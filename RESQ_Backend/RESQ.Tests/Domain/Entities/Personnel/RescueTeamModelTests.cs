using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Entities.Personnel.Exceptions;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Tests.Domain.Entities.Personnel;

/// <summary>
/// FE-02 – Rescuer Management: RescueTeamModel domain tests.
/// Covers: Create, AddMember, lifecycle transitions (Gathering → Available → OnMission etc.)
/// </summary>
public class RescueTeamModelTests
{
    private static readonly Guid ManagerId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");

    // -- Create --

    [Fact]
    public void Create_ReturnsTeam_WithStatusGathering()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, ManagerId);

        Assert.Equal("Alpha", team.Name);
        Assert.Equal(RescueTeamType.Rescue, team.TeamType);
        Assert.Equal(RescueTeamStatus.Gathering, team.Status);
        Assert.Equal(1, team.AssemblyPointId);
        Assert.Equal(ManagerId, team.ManagedBy);
        Assert.StartsWith("RT-", team.Code);
    }

    [Fact]
    public void Create_GeneratesCode_WithCorrectPrefix()
    {
        var team = RescueTeamModel.Create("Delta Force", RescueTeamType.Medical, 1, ManagerId);

        Assert.StartsWith("RT-DEL-", team.Code);
    }

    [Fact]
    public void Create_PadsShortName_WithX()
    {
        var team = RescueTeamModel.Create("AB", RescueTeamType.Rescue, 1, ManagerId);

        Assert.StartsWith("RT-ABX-", team.Code);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Create_AcceptsValidMaxMembers(int maxMembers)
    {
        var team = RescueTeamModel.Create("Test", RescueTeamType.Rescue, 1, ManagerId, maxMembers);

        Assert.Equal(maxMembers, team.MaxMembers);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(0)]
    public void Create_Throws_WhenMaxMembersOutOfRange(int maxMembers)
    {
        Assert.Throws<RescueTeamBusinessRuleException>(
            () => RescueTeamModel.Create("Test", RescueTeamType.Rescue, 1, ManagerId, maxMembers));
    }

    // -- AddMember --

    [Fact]
    public void AddMember_AddsSuccessfully_WhenGathering()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, ManagerId, 6);
        var userId = Guid.NewGuid();

        team.AddMember(userId, isLeader: true, "Core", null);

        Assert.Single(team.Members);
        Assert.Equal(userId, team.Members.First().UserId);
    }

    [Fact]
    public void AddMember_Throws_WhenExceedsMaxMembers()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, ManagerId, 6);
        team.AddMember(Guid.NewGuid(), true, "Core", null);
        for (var i = 1; i < 6; i++)
            team.AddMember(Guid.NewGuid(), false, "Support", null);

        Assert.Throws<RescueTeamBusinessRuleException>(
            () => team.AddMember(Guid.NewGuid(), false, "Support", null));
    }

    [Fact]
    public void AddMember_Throws_WhenDuplicateUser()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, ManagerId, 6);
        var userId = Guid.NewGuid();
        team.AddMember(userId, true, "Core", null);

        Assert.Throws<RescueTeamBusinessRuleException>(
            () => team.AddMember(userId, false, "Support", null));
    }

    [Fact]
    public void AddMember_Throws_WhenSecondLeaderAdded()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, ManagerId, 6);
        team.AddMember(Guid.NewGuid(), true, "Core", null);

        Assert.Throws<RescueTeamBusinessRuleException>(
            () => team.AddMember(Guid.NewGuid(), true, "Core", null));
    }

    // -- Lifecycle transitions --

    [Fact]
    public void SetAvailableByLeader_Transitions_FromGathering()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, ManagerId, 6);
        var leaderId = Guid.NewGuid();
        team.AddMember(leaderId, true, "Core", null);

        team.SetAvailableByLeader(leaderId);

        Assert.Equal(RescueTeamStatus.Available, team.Status);
    }

    [Fact]
    public void AssignMission_Throws_WhenNotAvailable()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, ManagerId, 6);

        Assert.Throws<InvalidTeamTransitionException>(
            () => team.AssignMission());
    }

    [Fact]
    public void AssignMission_Succeeds_WhenAvailable()
    {
        var team = CreateAvailableTeam();

        team.AssignMission();

        Assert.Equal(RescueTeamStatus.Assigned, team.Status);
    }

    [Fact]
    public void StartMission_Succeeds_WhenAssigned()
    {
        var team = CreateAvailableTeam();
        team.AssignMission();

        team.StartMission();

        Assert.Equal(RescueTeamStatus.OnMission, team.Status);
    }

    [Fact]
    public void FinishMission_ReturnsToAvailable()
    {
        var team = CreateAvailableTeam();
        team.AssignMission();
        team.StartMission();

        team.FinishMission();

        Assert.Equal(RescueTeamStatus.Available, team.Status);
    }

    [Fact]
    public void Disband_Succeeds_WhenGathering()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, ManagerId, 6);

        team.Disband();

        Assert.Equal(RescueTeamStatus.Disbanded, team.Status);
        Assert.NotNull(team.DisbandAt);
    }

    [Fact]
    public void Disband_Throws_WhenOnMission()
    {
        var team = CreateAvailableTeam();
        team.AssignMission();
        team.StartMission();

        Assert.Throws<InvalidTeamTransitionException>(
            () => team.Disband());
    }

    // -- Helper --

    private static RescueTeamModel CreateAvailableTeam()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, ManagerId, 6);
        var leaderId = Guid.NewGuid();
        team.AddMember(leaderId, true, "Core", null);
        team.SetAvailableByLeader(leaderId);
        return team;
    }
}
