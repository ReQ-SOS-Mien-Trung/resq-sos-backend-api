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
/// Luồng 10 – SOS đang xử lý → Incident → Redirect: SOS InProgress bị incident,
/// chuyển sang Incident, tạo cluster mới, hệ thống điều phối lại.
/// </summary>
public class Flow10_SosIncidentRedirectTests
{
    private static readonly Guid VictimId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid CoordinatorId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Guid LeaderId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

    // ────────── SOS status: InProgress → Incident ──────────

    [Fact]
    public void SosStatus_InProgress_To_Incident()
    {
        var sos = SosRequestModel.Create(VictimId, new GeoLocation(10.82, 106.63), "SOS ngập lụt");
        sos.SetStatus(SosRequestStatus.Assigned);
        sos.SetStatus(SosRequestStatus.InProgress);

        sos.SetStatus(SosRequestStatus.Incident);

        Assert.Equal(SosRequestStatus.Incident, sos.Status);
    }

    [Fact]
    public void SosStatus_Incident_CanBeReclustered()
    {
        // SOS ở trạng thái Incident hợp lệ để gom cluster mới
        var sos = SosRequestModel.Create(VictimId, new GeoLocation(10.82, 106.63), "SOS");
        sos.SetStatus(SosRequestStatus.InProgress);
        sos.SetStatus(SosRequestStatus.Incident);

        // Cluster handler accepts Pending OR Incident
        Assert.True(sos.Status == SosRequestStatus.Pending || sos.Status == SosRequestStatus.Incident);
    }

    [Fact]
    public void SosStatus_Incident_CanGoBackTo_Assigned()
    {
        // Sau khi gom cluster mới → tạo mission mới → SOS chuyển Assigned lại
        var sos = SosRequestModel.Create(VictimId, new GeoLocation(10.82, 106.63), "SOS");
        sos.SetStatus(SosRequestStatus.InProgress);
        sos.SetStatus(SosRequestStatus.Incident);

        sos.SetStatus(SosRequestStatus.Assigned);

        Assert.Equal(SosRequestStatus.Assigned, sos.Status);
    }

    // ────────── Mission goes Incompleted when team can't continue ──────────

    [Fact]
    public void MissionStateMachine_OnGoing_To_Incompleted()
    {
        MissionStateMachine.EnsureValidTransition(MissionStatus.OnGoing, MissionStatus.Incompleted);
    }

    [Fact]
    public void MissionStateMachine_Incompleted_Is_Terminal()
    {
        Assert.Throws<BadRequestException>(
            () => MissionStateMachine.EnsureValidTransition(
                MissionStatus.Incompleted, MissionStatus.OnGoing));
    }

    // ────────── Team incident with handover triggers redirect ──────────

    [Fact]
    public void TeamIncidentModel_HandoverDecision_ForRedirect()
    {
        var incident = new TeamIncidentModel
        {
            MissionTeamId = 1,
            IncidentScope = TeamIncidentScope.Mission,
            DecisionCode = "handover_mission",
            Description = "Đường bị cắt, team không thể tiếp cận khu vực",
            NeedSupportSos = true,
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
                }
            ]
        };

        Assert.Equal("handover_mission", incident.DecisionCode);
        Assert.True(incident.NeedSupportSos);
        Assert.Single(incident.AffectedActivities);
    }

    // ────────── Support SOS creation ──────────

    [Fact]
    public void SupportSos_CreatedDuringIncident_IsPending()
    {
        // Khi NeedSupportSos = true, handler tạo SOS mới → Pending
        var supportSos = SosRequestModel.Create(LeaderId, new GeoLocation(10.82, 106.63),
            "Yêu cầu hỗ trợ: team mắc kẹt tại khu vực ngập");

        Assert.Equal(SosRequestStatus.Pending, supportSos.Status);
    }

    [Fact]
    public void SupportSos_CanBeClustered_ForNewMission()
    {
        var supportSos = SosRequestModel.Create(LeaderId, new GeoLocation(10.82, 106.63), "Support SOS");

        // Pending → can be grouped into new cluster
        Assert.Equal(SosRequestStatus.Pending, supportSos.Status);
        Assert.False(supportSos.ClusterId.HasValue);
    }

    // ────────── Activities reset for handover ──────────

    [Fact]
    public void ActivityReset_Handover_PlannedRemains_OnGoingCancelled()
    {
        // Handover decision: OnGoing activities get cancelled, then recreated as Planned for new team
        // Existing Planned activities also get reset
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.OnGoing, MissionActivityStatus.Cancelled);
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.Planned, MissionActivityStatus.Cancelled);
    }

    [Fact]
    public void ActivityReset_SucceedActivities_NotAffected()
    {
        // Already completed activities are terminal, not affected by handover
        Assert.Throws<BadRequestException>(
            () => MissionActivityStateMachine.EnsureValidTransition(
                MissionActivityStatus.Succeed, MissionActivityStatus.Cancelled));
    }

    // ────────── Team state during redirect ──────────

    [Fact]
    public void RescueTeam_OnMission_ReportIncident_Stuck()
    {
        var team = CreateOnMissionTeam();
        team.ReportIncident();

        Assert.Equal(RescueTeamStatus.Stuck, team.Status);
    }

    [Fact]
    public void RescueTeam_Stuck_Resolved_Available_ForNewMission()
    {
        var team = CreateOnMissionTeam();
        team.ReportIncident();
        team.ResolveIncident(hasInjuredMember: false);

        Assert.Equal(RescueTeamStatus.Available, team.Status);
        // Can be assigned again
        team.AssignMission();
        Assert.Equal(RescueTeamStatus.Assigned, team.Status);
    }

    // ────────── Full Flow: SOS InProgress → Incident → Redirect ──────────

    [Fact]
    public void FullFlow_SosInProgress_Incident_NewCluster_NewMission()
    {
        // 1. Original SOS → Assigned → InProgress
        var originalSos = SosRequestModel.Create(VictimId, new GeoLocation(10.82, 106.63), "Ngập lụt cần cứu hộ");
        originalSos.SetStatus(SosRequestStatus.Assigned);
        originalSos.SetStatus(SosRequestStatus.InProgress);

        // 2. Mission Planned → OnGoing
        MissionStateMachine.EnsureValidTransition(MissionStatus.Planned, MissionStatus.OnGoing);

        // 3. Team encounters incident → Stuck
        var team = CreateOnMissionTeam();
        team.ReportIncident();
        Assert.Equal(RescueTeamStatus.Stuck, team.Status);

        // 4. Incident reported (handover)
        var incident = new TeamIncidentModel
        {
            MissionTeamId = 1,
            IncidentScope = TeamIncidentScope.Mission,
            DecisionCode = "handover_mission",
            NeedSupportSos = true,
            Status = TeamIncidentStatus.Reported,
            ReportedBy = LeaderId,
            ReportedAt = DateTime.UtcNow
        };
        Assert.Equal(TeamIncidentStatus.Reported, incident.Status);

        // 5. SOS → Incident
        originalSos.SetStatus(SosRequestStatus.Incident);
        Assert.Equal(SosRequestStatus.Incident, originalSos.Status);

        // 6. Mission → Incompleted
        MissionStateMachine.EnsureValidTransition(MissionStatus.OnGoing, MissionStatus.Incompleted);

        // 7. Old team resolved
        team.ResolveIncident(hasInjuredMember: false);
        Assert.Equal(RescueTeamStatus.Available, team.Status);

        // 8. SOS (Incident) can be regrouped into new cluster
        Assert.Equal(SosRequestStatus.Incident, originalSos.Status);

        // 9. New cluster → New mission
        var newCluster = new SosClusterModel
        {
            Id = 2,
            SosRequestIds = [originalSos.Id],
            IsMissionCreated = false
        };
        Assert.False(newCluster.IsMissionCreated);

        // 10. SOS → Assigned again
        originalSos.SetStatus(SosRequestStatus.Assigned);
        Assert.Equal(SosRequestStatus.Assigned, originalSos.Status);

        // 11. New mission planned and started
        MissionStateMachine.EnsureValidTransition(MissionStatus.Planned, MissionStatus.OnGoing);
    }

    [Fact]
    public void FullFlow_IncidentResolved_SosGoesBackToResolved()
    {
        var sos = SosRequestModel.Create(VictimId, new GeoLocation(10.82, 106.63), "SOS");
        sos.SetStatus(SosRequestStatus.Assigned);
        sos.SetStatus(SosRequestStatus.InProgress);
        sos.SetStatus(SosRequestStatus.Incident);
        sos.SetStatus(SosRequestStatus.Assigned); // re-clustered
        sos.SetStatus(SosRequestStatus.InProgress); // new mission started
        sos.SetStatus(SosRequestStatus.Resolved); // finally resolved

        Assert.Equal(SosRequestStatus.Resolved, sos.Status);
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
