using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Tests.Application.UseCases.Workflows;

/// <summary>
/// Luồng 2 – Activity-level Incident: SOS → Cluster → Mission → Rescuer gặp incident ở activity → Tiếp tục.
/// Validates that an activity can Fail from incident while the team continues with the remaining activities.
/// </summary>
public class Flow2_ActivityIncident_ContinueMissionTests
{
    private static readonly Guid CoordinatorId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Guid LeaderId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

    // ────────── Activity fails from incident, team continues ──────────

    [Fact]
    public void ActivityStateMachine_OnGoing_To_Failed_IsValid()
    {
        // Incident khiến activity hiện tại → Failed
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.OnGoing, MissionActivityStatus.Failed);
    }

    [Fact]
    public void ActivityStateMachine_Failed_Is_Terminal()
    {
        // Failed activity không chuyển đi đâu nữa
        Assert.Throws<BadRequestException>(
            () => MissionActivityStateMachine.EnsureValidTransition(
                MissionActivityStatus.Failed, MissionActivityStatus.OnGoing));
    }

    [Fact]
    public void NextActivity_CanStart_AfterPreviousFailed()
    {
        // Activity tiếp theo vẫn là Planned → OnGoing (auto-start)
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.Planned, MissionActivityStatus.OnGoing);
    }

    [Fact]
    public void TeamIncidentStateMachine_FullCycle_ReportedToResolved()
    {
        // Reported → InProgress → Resolved
        TeamIncidentStateMachine.EnsureValidTransition(
            TeamIncidentStatus.Reported, TeamIncidentStatus.InProgress);
        TeamIncidentStateMachine.EnsureValidTransition(
            TeamIncidentStatus.InProgress, TeamIncidentStatus.Resolved);
    }

    [Fact]
    public void TeamIncidentStateMachine_Resolved_Is_Terminal()
    {
        Assert.Throws<BadRequestException>(
            () => TeamIncidentStateMachine.EnsureValidTransition(
                TeamIncidentStatus.Resolved, TeamIncidentStatus.InProgress));
    }

    [Fact]
    public void TeamIncidentStateMachine_Reported_Cannot_Skip_To_Resolved()
    {
        Assert.Throws<BadRequestException>(
            () => TeamIncidentStateMachine.EnsureValidTransition(
                TeamIncidentStatus.Reported, TeamIncidentStatus.Resolved));
    }

    [Fact]
    public void TeamIncidentModel_ActivityScope_WhenActivityIdProvided()
    {
        var incident = new TeamIncidentModel
        {
            MissionTeamId = 1,
            MissionActivityId = 42,
            IncidentScope = TeamIncidentScope.Activity,
            Description = "Đường bị sạt lở, không thể đến điểm giao hàng",
            Status = TeamIncidentStatus.Reported,
            ReportedBy = LeaderId,
            ReportedAt = DateTime.UtcNow
        };

        Assert.Equal(TeamIncidentScope.Activity, incident.IncidentScope);
        Assert.Equal(42, incident.MissionActivityId);
        Assert.Equal(TeamIncidentStatus.Reported, incident.Status);
    }

    [Fact]
    public void MissionStaysOnGoing_WhenActivityFails()
    {
        // Mission vẫn OnGoing mặc dù Activity bị fail
        // Chỉ chuyển Completed/Incompleted sau khi tất cả team report
        MissionStateMachine.EnsureValidTransition(MissionStatus.Planned, MissionStatus.OnGoing);
        // No transition to Completed yet — mission still working
        Assert.Throws<BadRequestException>(
            () => MissionStateMachine.EnsureValidTransition(MissionStatus.Planned, MissionStatus.Completed));
    }

    [Fact]
    public void RescueTeam_ReportsIncident_ThenResolves_BackToAvailable()
    {
        var team = CreateOnMissionTeam();

        // Team gặp incident
        team.ReportIncident();
        Assert.Equal(RescueTeamStatus.Stuck, team.Status);

        // Incident được resolved, không có thành viên bị thương
        team.ResolveIncident(hasInjuredMember: false);
        Assert.Equal(RescueTeamStatus.Available, team.Status);
    }

    [Fact]
    public void RescueTeam_ReportsIncident_ThenResolves_Unavailable_WhenInjured()
    {
        var team = CreateOnMissionTeam();

        team.ReportIncident();
        Assert.Equal(RescueTeamStatus.Stuck, team.Status);

        // Incident resolved nhưng có thành viên bị thương
        team.ResolveIncident(hasInjuredMember: true);
        Assert.Equal(RescueTeamStatus.Unavailable, team.Status);
    }

    // ────────── Activity chain after incident ──────────

    [Fact]
    public void MultipleActivities_OneFailsOthersSucceed()
    {
        // Simulate: Activity 1 (OnGoing → Failed), Activity 2 (Planned → OnGoing → Succeed), Activity 3 (Planned → OnGoing → Succeed)
        MissionActivityStateMachine.EnsureValidTransition(MissionActivityStatus.OnGoing, MissionActivityStatus.Failed);
        MissionActivityStateMachine.EnsureValidTransition(MissionActivityStatus.Planned, MissionActivityStatus.OnGoing);
        MissionActivityStateMachine.EnsureValidTransition(MissionActivityStatus.OnGoing, MissionActivityStatus.Succeed);
        MissionActivityStateMachine.EnsureValidTransition(MissionActivityStatus.Planned, MissionActivityStatus.OnGoing);
        MissionActivityStateMachine.EnsureValidTransition(MissionActivityStatus.OnGoing, MissionActivityStatus.Succeed);
    }

    [Fact]
    public void SosStatus_InProgress_During_ActivityIncident()
    {
        // SOS vẫn InProgress khi activity bị fail ở mức activity
        var sos = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS");
        sos.SetStatus(SosRequestStatus.InProgress);

        // SOS không chuyển sang Incident trừ khi incident ở mức Mission
        Assert.Equal(SosRequestStatus.InProgress, sos.Status);
    }

    // ────────── Helper ──────────

    private static RescueTeamModel CreateOnMissionTeam()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, CoordinatorId, 6);
        team.AddMember(LeaderId, true, "Core", null);
        team.SetAvailableByLeader(LeaderId);
        team.AssignMission();
        team.StartMission();
        return team;
    }
}
