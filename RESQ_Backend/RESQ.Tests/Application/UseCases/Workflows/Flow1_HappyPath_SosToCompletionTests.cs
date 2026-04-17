using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Entities.Personnel.Exceptions;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Tests.Application.UseCases.Workflows;

/// <summary>
/// Luồng 1 – Happy Path: SOS → Cluster → Mission → Team thực hiện → Hoàn thành → Report.
/// Tests state machine transitions + domain model behaviour across the entire lifecycle.
/// </summary>
public class Flow1_HappyPath_SosToCompletionTests
{
    private static readonly Guid VictimId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid CoordinatorId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Guid LeaderId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

    // ────────── Step 1: SOS Request creation ──────────

    [Fact]
    public void Step1_CreateSosRequest_DefaultStatusIsPending()
    {
        var sos = SosRequestModel.Create(
            VictimId,
            new GeoLocation(10.82, 106.63),
            "Ngập lụt nghiêm trọng, cần cứu hộ gấp!");

        Assert.Equal(SosRequestStatus.Pending, sos.Status);
        Assert.Equal(VictimId, sos.UserId);
        Assert.NotNull(sos.CreatedAt);
    }

    [Fact]
    public void Step1_SetPriority_StoresLevel()
    {
        var sos = SosRequestModel.Create(VictimId, new GeoLocation(10.82, 106.63), "Cần cứu hộ");
        sos.SetPriorityLevel(SosPriorityLevel.Critical);

        Assert.Equal(SosPriorityLevel.Critical, sos.PriorityLevel);
    }

    // ────────── Step 2: Coordinator gom cluster ──────────

    [Fact]
    public void Step2_ClusterModel_InitialState()
    {
        var cluster = new SosClusterModel
        {
            Id = 1,
            CenterLatitude = 10.82,
            CenterLongitude = 106.63,
            SeverityLevel = "Critical",
            SosRequestIds = [101, 102, 103]
        };

        Assert.Equal(SosClusterStatus.Pending, cluster.Status);
        Assert.Equal(3, cluster.SosRequestIds.Count);
    }

    [Fact]
    public void Step2_SosStatus_Pending_To_Assigned_AfterClustering()
    {
        var sos = SosRequestModel.Create(VictimId, new GeoLocation(10.82, 106.63), "SOS");
        Assert.Equal(SosRequestStatus.Pending, sos.Status);

        // Hệ thống đánh dấu SOS → Assigned khi Mission được tạo từ cluster
        sos.SetStatus(SosRequestStatus.Assigned);
        Assert.Equal(SosRequestStatus.Assigned, sos.Status);
    }

    // ────────── Step 3: Tạo Mission (Planned) ──────────

    [Fact]
    public void Step3_MissionModel_DefaultStatusIsPlanned()
    {
        var mission = new MissionModel
        {
            Id = 1,
            ClusterId = 1,
            MissionType = "Flood Relief",
            PriorityScore = 90,
            CreatedById = CoordinatorId,
            CreatedAt = DateTime.UtcNow
        };

        Assert.Equal(MissionStatus.Planned, mission.Status);
        Assert.Null(mission.IsCompleted);
    }

    [Fact]
    public void Step3_MissionStateMachine_Planned_To_OnGoing_IsValid()
    {
        // Should not throw
        MissionStateMachine.EnsureValidTransition(MissionStatus.Planned, MissionStatus.OnGoing);
    }

    [Fact]
    public void Step3_MissionStateMachine_Planned_To_Completed_IsInvalid()
    {
        Assert.Throws<BadRequestException>(
            () => MissionStateMachine.EnsureValidTransition(MissionStatus.Planned, MissionStatus.Completed));
    }

    // ────────── Step 4: Coordinator khởi chạy Mission (OnGoing) ──────────

    [Fact]
    public void Step4_SosStatus_Assigned_To_InProgress_WhenMissionOnGoing()
    {
        var sos = SosRequestModel.Create(VictimId, new GeoLocation(10.82, 106.63), "SOS");
        sos.SetStatus(SosRequestStatus.Assigned);

        // Hệ thống đổi SOS → InProgress khi Mission chuyển OnGoing
        sos.SetStatus(SosRequestStatus.InProgress);
        Assert.Equal(SosRequestStatus.InProgress, sos.Status);
    }

    [Fact]
    public void Step4_ActivityStateMachine_Planned_To_OnGoing_IsValid()
    {
        // Auto-start first activities per team
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.Planned, MissionActivityStatus.OnGoing);
    }

    // ────────── Step 5: Rescuer thực hiện activities ──────────

    [Fact]
    public void Step5_ActivityStateMachine_OnGoing_To_Succeed_IsValid()
    {
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.OnGoing, MissionActivityStatus.Succeed);
    }

    [Fact]
    public void Step5_ActivityStateMachine_OnGoing_To_PendingConfirmation_IsValid()
    {
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.OnGoing, MissionActivityStatus.PendingConfirmation);
    }

    [Fact]
    public void Step5_ActivityStateMachine_PendingConfirmation_To_Succeed_IsValid()
    {
        MissionActivityStateMachine.EnsureValidTransition(
            MissionActivityStatus.PendingConfirmation, MissionActivityStatus.Succeed);
    }

    [Fact]
    public void Step5_RescueTeam_FullLifecycle_Gathering_To_OnMission()
    {
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, CoordinatorId, 6);
        Assert.Equal(RescueTeamStatus.Gathering, team.Status);

        // Leader confirms team gathered
        team.AddMember(LeaderId, true, "Core", null);
        team.SetAvailableByLeader(LeaderId);
        Assert.Equal(RescueTeamStatus.Available, team.Status);

        // Coordinator assigns mission
        team.AssignMission();
        Assert.Equal(RescueTeamStatus.Assigned, team.Status);

        // Mission starts
        team.StartMission();
        Assert.Equal(RescueTeamStatus.OnMission, team.Status);
    }

    // ────────── Step 6: Team hoàn thành Mission ──────────

    [Fact]
    public void Step6_RescueTeam_OnMission_To_Available_AfterFinish()
    {
        var team = CreateOnMissionTeam();

        team.FinishMission();

        Assert.Equal(RescueTeamStatus.Available, team.Status);
    }

    [Fact]
    public void Step6_MissionStateMachine_OnGoing_To_Completed_IsValid()
    {
        MissionStateMachine.EnsureValidTransition(MissionStatus.OnGoing, MissionStatus.Completed);
    }

    [Fact]
    public void Step6_MissionStateMachine_Completed_Is_Terminal()
    {
        Assert.Throws<BadRequestException>(
            () => MissionStateMachine.EnsureValidTransition(MissionStatus.Completed, MissionStatus.OnGoing));
    }

    // ────────── Step 7: Full lifecycle in sequence ──────────

    [Fact]
    public void FullLifecycle_AllMissionStateMachineTransitions()
    {
        // Planned → OnGoing (should not throw)
        MissionStateMachine.EnsureValidTransition(MissionStatus.Planned, MissionStatus.OnGoing);
        // OnGoing → Completed (should not throw)  
        MissionStateMachine.EnsureValidTransition(MissionStatus.OnGoing, MissionStatus.Completed);
    }

    [Fact]
    public void FullLifecycle_AllActivityStateMachineTransitions_HappyPath()
    {
        // Planned → OnGoing → Succeed
        MissionActivityStateMachine.EnsureValidTransition(MissionActivityStatus.Planned, MissionActivityStatus.OnGoing);
        MissionActivityStateMachine.EnsureValidTransition(MissionActivityStatus.OnGoing, MissionActivityStatus.Succeed);
    }

    [Fact]
    public void FullLifecycle_SosStatus_Progression()
    {
        var sos = SosRequestModel.Create(VictimId, new GeoLocation(10.82, 106.63), "SOS");
        Assert.Equal(SosRequestStatus.Pending, sos.Status);

        sos.SetStatus(SosRequestStatus.Assigned);
        Assert.Equal(SosRequestStatus.Assigned, sos.Status);

        sos.SetStatus(SosRequestStatus.InProgress);
        Assert.Equal(SosRequestStatus.InProgress, sos.Status);

        sos.SetStatus(SosRequestStatus.Resolved);
        Assert.Equal(SosRequestStatus.Resolved, sos.Status);
    }

    [Fact]
    public void FullLifecycle_RescueTeam_CompleteJourney()
    {
        // Gathering → Available → Assigned → OnMission → Available (finish)
        var team = RescueTeamModel.Create("Alpha", RescueTeamType.Rescue, 1, CoordinatorId, 6);
        team.AddMember(LeaderId, true, "Core", null);
        team.SetAvailableByLeader(LeaderId);
        team.AssignMission();
        team.StartMission();
        team.FinishMission();

        Assert.Equal(RescueTeamStatus.Available, team.Status);
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
