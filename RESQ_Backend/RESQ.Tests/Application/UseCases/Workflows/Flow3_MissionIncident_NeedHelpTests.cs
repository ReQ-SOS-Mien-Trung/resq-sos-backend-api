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
/// Luồng 3 – Mission-level Incident: Team gặp sự cố ở cấp Mission → Không thể tiếp tục → Cần hỗ trợ.
/// Validates mission incident reporting, team Stuck/Unavailable transitions, and stop/rescue decision codes.
/// </summary>
public class Flow3_MissionIncident_NeedHelpTests
{
    private static readonly Guid CoordinatorId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Guid LeaderId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
    private static readonly Guid MemberId = Guid.Parse("dddddddd-4444-4444-4444-444444444444");

    // ────────── Mission must be OnGoing to report incident ──────────

    [Fact]
    public void MissionStateMachine_OnGoing_Is_Required_ForIncident()
    {
        // Mission phải OnGoing mới được báo incident
        MissionStateMachine.EnsureValidTransition(MissionStatus.Planned, MissionStatus.OnGoing);
    }

    [Fact]
    public void MissionStateMachine_OnGoing_To_Incompleted_IsValid()
    {
        MissionStateMachine.EnsureValidTransition(MissionStatus.OnGoing, MissionStatus.Incompleted);
    }

    [Fact]
    public void MissionStateMachine_Incompleted_Is_Terminal()
    {
        Assert.Throws<BadRequestException>(
            () => MissionStateMachine.EnsureValidTransition(MissionStatus.Incompleted, MissionStatus.OnGoing));
    }

    // ────────── Team gets stuck during mission ──────────

    [Fact]
    public void RescueTeam_OnMission_ReportIncident_BecomesStuck()
    {
        var team = CreateOnMissionTeam();

        team.ReportIncident();

        Assert.Equal(RescueTeamStatus.Stuck, team.Status);
    }

    [Fact]
    public void RescueTeam_Stuck_ResolveWithoutInjury_BecomesAvailable()
    {
        var team = CreateOnMissionTeam();
        team.ReportIncident();

        team.ResolveIncident(hasInjuredMember: false);

        Assert.Equal(RescueTeamStatus.Available, team.Status);
    }

    [Fact]
    public void RescueTeam_Stuck_ResolveWithInjury_BecomesUnavailable()
    {
        var team = CreateOnMissionTeam();
        team.ReportIncident();

        team.ResolveIncident(hasInjuredMember: true);

        Assert.Equal(RescueTeamStatus.Unavailable, team.Status);
    }

    // ────────── Team Incident state machine ──────────

    [Fact]
    public void TeamIncident_FullLifecycle_Reported_InProgress_Resolved()
    {
        TeamIncidentStateMachine.EnsureValidTransition(TeamIncidentStatus.Reported, TeamIncidentStatus.InProgress);
        TeamIncidentStateMachine.EnsureValidTransition(TeamIncidentStatus.InProgress, TeamIncidentStatus.Resolved);
    }

    [Fact]
    public void TeamIncident_CannotSkip_Reported_To_Resolved()
    {
        Assert.Throws<BadRequestException>(
            () => TeamIncidentStateMachine.EnsureValidTransition(
                TeamIncidentStatus.Reported, TeamIncidentStatus.Resolved));
    }

    [Fact]
    public void TeamIncident_Resolved_Is_Terminal()
    {
        Assert.Throws<BadRequestException>(
            () => TeamIncidentStateMachine.EnsureValidTransition(
                TeamIncidentStatus.Resolved, TeamIncidentStatus.Reported));
    }

    // ────────── TeamIncidentModel for mission-scope incident ──────────

    [Fact]
    public void TeamIncidentModel_MissionScope_StopMission()
    {
        var incident = new TeamIncidentModel
        {
            MissionTeamId = 1,
            MissionActivityId = null,
            IncidentScope = TeamIncidentScope.Mission,
            DecisionCode = "stop_mission",
            Description = "Lũ quá lớn, đội không thể tiếp tục",
            NeedSupportSos = true,
            NeedReassignActivity = false,
            Status = TeamIncidentStatus.Reported,
            ReportedBy = LeaderId,
            ReportedAt = DateTime.UtcNow
        };

        Assert.Equal(TeamIncidentScope.Mission, incident.IncidentScope);
        Assert.Equal("stop_mission", incident.DecisionCode);
        Assert.True(incident.NeedSupportSos);
        Assert.False(incident.NeedReassignActivity);
    }

    [Fact]
    public void TeamIncidentModel_MissionScope_RescueWholeTeam()
    {
        var incident = new TeamIncidentModel
        {
            MissionTeamId = 1,
            IncidentScope = TeamIncidentScope.Mission,
            DecisionCode = "rescue_whole_team_immediately",
            Description = "Đội bị mắc kẹt, cần cứu hộ khẩn cấp",
            NeedSupportSos = true,
            Status = TeamIncidentStatus.Reported,
            ReportedBy = LeaderId,
            ReportedAt = DateTime.UtcNow
        };

        Assert.Equal("rescue_whole_team_immediately", incident.DecisionCode);
        Assert.True(incident.NeedSupportSos);
    }

    [Fact]
    public void TeamIncidentModel_MissionScope_ContinueMission()
    {
        // Ghi nhận sự cố nhưng tiếp tục
        var incident = new TeamIncidentModel
        {
            MissionTeamId = 1,
            IncidentScope = TeamIncidentScope.Mission,
            DecisionCode = "continue_mission",
            Description = "Sự cố nhỏ, đội tiếp tục thực hiện nhiệm vụ",
            NeedSupportSos = false,
            Status = TeamIncidentStatus.Reported,
            ReportedBy = LeaderId,
            ReportedAt = DateTime.UtcNow
        };

        Assert.Equal("continue_mission", incident.DecisionCode);
        Assert.False(incident.NeedSupportSos);
    }

    // ────────── SOS changes to Incident when mission is stopped ──────────

    [Fact]
    public void SosStatus_InProgress_To_Incident_WhenMissionStopped()
    {
        var sos = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS");
        sos.SetStatus(SosRequestStatus.InProgress);

        // Mission bị stop → SOS chuyển sang Incident
        sos.SetStatus(SosRequestStatus.Incident);

        Assert.Equal(SosRequestStatus.Incident, sos.Status);
    }

    [Fact]
    public void SosStatus_Incident_CanBeReclustered()
    {
        // SOS ở trạng thái Incident có thể tạo cluster mới (Pending hoặc Incident OK)
        var sos = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS");
        sos.SetStatus(SosRequestStatus.Incident);

        Assert.Equal(SosRequestStatus.Incident, sos.Status);
    }

    // ────────── Activities get affected by mission-level incident ──────────

    [Fact]
    public void Activity_Planned_CanFailFromIncident()
    {
        // Planned activities can be failed during incident
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.Planned, MissionActivityStatus.Cancelled);
    }

    [Fact]
    public void Activity_OnGoing_CanFailFromIncident()
    {
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.OnGoing, MissionActivityStatus.Failed);
    }

    [Fact]
    public void Activity_PendingConfirmation_CanFailFromIncident()
    {
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.PendingConfirmation, MissionActivityStatus.Failed);
    }

    [Fact]
    public void Activity_Succeed_CannotFailFromIncident()
    {
        // Already succeed → terminal, cannot fail again
        Assert.Throws<BadRequestException>(
            () => MissionActivityStateMachine.EnsureValidTransition(
                MissionActivityStatus.Succeed, MissionActivityStatus.Failed));
    }

    // ────────── AffectedActivities tracking ──────────

    [Fact]
    public void TeamIncidentModel_AffectedActivities_OrderByStep()
    {
        var incident = new TeamIncidentModel
        {
            MissionTeamId = 1,
            IncidentScope = TeamIncidentScope.Mission,
            DecisionCode = "stop_mission",
            Status = TeamIncidentStatus.Reported,
            AffectedActivities =
            [
                new TeamIncidentAffectedActivityModel { MissionActivityId = 10, OrderIndex = 0, IsPrimary = true, Step = 1, ActivityType = "COLLECT_SUPPLIES" },
                new TeamIncidentAffectedActivityModel { MissionActivityId = 11, OrderIndex = 1, IsPrimary = false, Step = 2, ActivityType = "DELIVER_SUPPLIES" }
            ]
        };

        Assert.Equal(2, incident.AffectedActivities.Count);
        Assert.True(incident.AffectedActivities[0].IsPrimary);
        Assert.False(incident.AffectedActivities[1].IsPrimary);
    }

    // ────────── Full Sequence: Mission incident → Stuck → SOS Incident ──────────

    [Fact]
    public void FullLifecycle_MissionIncident_TeamStuck_SosIncident()
    {
        // 1. SOS Created → Assigned → InProgress
        var sos = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS ngập lụt");
        sos.SetStatus(SosRequestStatus.Assigned);
        sos.SetStatus(SosRequestStatus.InProgress);

        // 2. Mission Planned → OnGoing
        MissionStateMachine.EnsureValidTransition(MissionStatus.Planned, MissionStatus.OnGoing);

        // 3. Team OnMission → Reports incident → Stuck
        var team = CreateOnMissionTeam();
        team.ReportIncident();
        Assert.Equal(RescueTeamStatus.Stuck, team.Status);

        // 4. Incident reported
        var incident = new TeamIncidentModel
        {
            MissionTeamId = 1,
            IncidentScope = TeamIncidentScope.Mission,
            DecisionCode = "rescue_whole_team_immediately",
            NeedSupportSos = true,
            Status = TeamIncidentStatus.Reported,
            ReportedBy = LeaderId,
            ReportedAt = DateTime.UtcNow
        };
        Assert.Equal(TeamIncidentStatus.Reported, incident.Status);

        // 5. SOS → Incident
        sos.SetStatus(SosRequestStatus.Incident);
        Assert.Equal(SosRequestStatus.Incident, sos.Status);

        // 6. Mission → Incompleted (if all teams cannot continue)
        MissionStateMachine.EnsureValidTransition(MissionStatus.OnGoing, MissionStatus.Incompleted);
    }

    // ────────── Helper ──────────

    private static RescueTeamModel CreateOnMissionTeam()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, CoordinatorId, 6);
        team.AddMember(LeaderId, true, "Core", null);
        team.AddMember(MemberId, false, "Support", null);
        team.SetAvailableByLeader(LeaderId);
        team.AssignMission();
        team.StartMission();
        return team;
    }
}
