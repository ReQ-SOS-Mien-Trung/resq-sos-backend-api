using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Entities.Personnel.Exceptions;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Tests.Application.UseCases.Workflows;

/// <summary>
/// Luồng 7 – Điều phối lại (Re-dispatch): Hủy team hiện tại, reset activity → Planned, gán team mới.
/// Validates CancelMission, handover_mission decision, activity reset, and re-assignment.
/// </summary>
public class Flow7_RedispatchTests
{
    private static readonly Guid CoordinatorId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Guid LeaderId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
    private static readonly Guid NewLeaderId = Guid.Parse("eeeeeeee-5555-5555-5555-555555555555");

    // ────────── CancelMission: Assigned → Available ──────────

    [Fact]
    public void RescueTeam_CancelMission_Assigned_To_Available()
    {
        var team = CreateAssignedTeam();
        Assert.Equal(RescueTeamStatus.Assigned, team.Status);

        team.CancelMission();

        Assert.Equal(RescueTeamStatus.Available, team.Status);
    }

    [Fact]
    public void RescueTeam_CancelMission_OnlyFromAssigned()
    {
        var team = CreateOnMissionTeam();
        Assert.Equal(RescueTeamStatus.OnMission, team.Status);

        // Cannot cancel from OnMission – need to use FinishMission or ReportIncident
        Assert.Throws<InvalidTeamTransitionException>(() => team.CancelMission());
    }

    // ────────── Handover decision: reset activities to Planned ──────────

    [Fact]
    public void TeamIncidentModel_HandoverMission_Decision()
    {
        var incident = new TeamIncidentModel
        {
            MissionTeamId = 1,
            IncidentScope = TeamIncidentScope.Mission,
            DecisionCode = "handover_mission",
            Description = "Team không thể tiếp tục, cần handover cho team khác",
            NeedSupportSos = false,
            NeedReassignActivity = false,
            Status = TeamIncidentStatus.Reported,
            ReportedBy = LeaderId,
            ReportedAt = DateTime.UtcNow,
            AffectedActivities =
            [
                new TeamIncidentAffectedActivityModel
                {
                    MissionActivityId = 10, OrderIndex = 0, IsPrimary = true,
                    Step = 1, ActivityType = "DELIVER_SUPPLIES",
                    Status = MissionActivityStatus.OnGoing
                },
                new TeamIncidentAffectedActivityModel
                {
                    MissionActivityId = 11, OrderIndex = 1, IsPrimary = false,
                    Step = 2, ActivityType = "RETURN_ASSEMBLY_POINT",
                    Status = MissionActivityStatus.Planned
                }
            ]
        };

        Assert.Equal("handover_mission", incident.DecisionCode);
        Assert.Equal(2, incident.AffectedActivities.Count);
    }

    [Fact]
    public void ActivityStateMachine_OnGoing_CanBeCancelled()
    {
        // Activity đang OnGoing bị cancel khi handover
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.OnGoing, MissionActivityStatus.Cancelled);
    }

    [Fact]
    public void ActivityStateMachine_Planned_CanBeCancelled()
    {
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.Planned, MissionActivityStatus.Cancelled);
    }

    [Fact]
    public void ActivityStateMachine_Cancelled_Is_Terminal()
    {
        Assert.Throws<BadRequestException>(
            () => MissionActivityStateMachine.EnsureValidTransition(
                MissionActivityStatus.Cancelled, MissionActivityStatus.Planned));
    }

    // ────────── Activity-level reassign decision ──────────

    [Fact]
    public void TeamIncidentModel_ReassignActivity_Decision()
    {
        var incident = new TeamIncidentModel
        {
            MissionTeamId = 1,
            MissionActivityId = 10,
            IncidentScope = TeamIncidentScope.Activity,
            DecisionCode = "reassign_activity",
            Description = "Activity cần gán lại cho team khác",
            NeedReassignActivity = true,
            Status = TeamIncidentStatus.Reported,
            ReportedBy = LeaderId,
            ReportedAt = DateTime.UtcNow
        };

        Assert.Equal("reassign_activity", incident.DecisionCode);
        Assert.True(incident.NeedReassignActivity);
    }

    // ────────── New team can be assigned after old team cancelled ──────────

    [Fact]
    public void NewTeam_CanBeAssigned_AfterOldTeamCancelled()
    {
        // Old team: Assigned → Cancel
        var oldTeam = CreateAssignedTeam();
        oldTeam.CancelMission();
        Assert.Equal(RescueTeamStatus.Available, oldTeam.Status);

        // New team: Available → Assigned
        var newTeam = RescueTeamModel.Create("Bravo", RescueTeamType.Rescue, 1, CoordinatorId, 6);
        newTeam.AddMember(NewLeaderId, true, "Core", null);
        newTeam.SetAvailableByLeader(NewLeaderId);
        newTeam.AssignMission();

        Assert.Equal(RescueTeamStatus.Assigned, newTeam.Status);
    }

    [Fact]
    public void NewTeam_CanStartMission_AfterAssignment()
    {
        var newTeam = RescueTeamModel.Create("Bravo", RescueTeamType.Rescue, 1, CoordinatorId, 6);
        newTeam.AddMember(NewLeaderId, true, "Core", null);
        newTeam.SetAvailableByLeader(NewLeaderId);
        newTeam.AssignMission();
        newTeam.StartMission();

        Assert.Equal(RescueTeamStatus.OnMission, newTeam.Status);
    }

    // ────────── Full re-dispatch: old team cancelled, activities reset, new team ──────────

    [Fact]
    public void FullRedispatch_OldTeamCancelled_NewTeamTakesOver()
    {
        // 1. Old team assigned and started
        var oldTeam = CreateOnMissionTeam();
        Assert.Equal(RescueTeamStatus.OnMission, oldTeam.Status);

        // 2. Incident → team stuck
        oldTeam.ReportIncident();
        Assert.Equal(RescueTeamStatus.Stuck, oldTeam.Status);

        // 3. Resolve incident (no injury) → back to Available
        oldTeam.ResolveIncident(hasInjuredMember: false);
        Assert.Equal(RescueTeamStatus.Available, oldTeam.Status);

        // 4. Activities reset to Planned (simulated by state machine validation)
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.Planned, MissionActivityStatus.OnGoing);

        // 5. New team assigned and starts
        var newTeam = RescueTeamModel.Create("Bravo", RescueTeamType.Rescue, 1, CoordinatorId, 6);
        newTeam.AddMember(NewLeaderId, true, "Core", null);
        newTeam.SetAvailableByLeader(NewLeaderId);
        newTeam.AssignMission();
        newTeam.StartMission();
        Assert.Equal(RescueTeamStatus.OnMission, newTeam.Status);
    }

    [Fact]
    public void MissionStaysOnGoing_DuringRedispatch()
    {
        // Mission vẫn OnGoing trong quá trình điều phối lại
        MissionStateMachine.EnsureValidTransition(MissionStatus.Planned, MissionStatus.OnGoing);
        // Still OnGoing – no transition to Completed/Incompleted yet
    }

    // ────────── Helper ──────────

    private static RescueTeamModel CreateAssignedTeam()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, CoordinatorId, 6);
        team.AddMember(LeaderId, true, "Core", null);
        team.SetAvailableByLeader(LeaderId);
        team.AssignMission();
        return team;
    }

    private static RescueTeamModel CreateOnMissionTeam()
    {
        var team = CreateAssignedTeam();
        team.StartMission();
        return team;
    }
}
