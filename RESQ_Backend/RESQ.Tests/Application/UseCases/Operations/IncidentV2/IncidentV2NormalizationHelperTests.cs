using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.Operations.Shared;

namespace RESQ.Tests.Application.UseCases.Operations.IncidentV2;

public class IncidentV2NormalizationHelperTests
{
    // ── helpers ────────────────────────────────────────────────────────────────

    private static MissionIncidentReportRequest ValidMissionRequest(
        string decision = IncidentV2Constants.MissionDecisionCodes.ContinueMission,
        int missionId = 1, int missionTeamId = 2) => new()
    {
        Scope = "Mission",
        Context = new MissionIncidentContextDto
        {
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            Location = new GeoLocationDto { Latitude = 10.0, Longitude = 106.0 }
        },
        MissionDecision = decision
    };

    private static ActivityIncidentReportRequest ValidActivityRequest(
        int missionId = 1, int missionTeamId = 2,
        bool canContinue = true, bool needReassign = false, bool needSos = false,
        int firstActivityId = 11) => new()
    {
        Scope = "Activity",
        Context = new ActivityIncidentContextDto
        {
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            Location = new GeoLocationDto { Latitude = 10.0, Longitude = 106.0 },
            Activities = [new ActivitySnapshotDto { ActivityId = firstActivityId }]
        },
        IncidentType = "vehicle_damage",
        Impact = new ActivityImpactDto
        {
            CanContinueActivity = canContinue,
            NeedReassignActivity = needReassign,
            NeedSupportSOS = needSos
        }
    };

    // ── contract binding / scope-mismatch tests ────────────────────────────────

    [Fact]
    public void NormalizeMissionRequest_ScopeMismatch_Returns400()
    {
        var request = ValidMissionRequest();
        request.Scope = "Activity"; // wrong scope

        Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request));
    }

    [Fact]
    public void NormalizeMissionRequest_ContextMissionIdMismatch_Returns400()
    {
        var request = ValidMissionRequest(missionId: 99); // context says 99, route says 1

        Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request));
    }

    [Fact]
    public void NormalizeMissionRequest_ContextMissionTeamIdMismatch_Returns400()
    {
        var request = ValidMissionRequest(missionTeamId: 77); // context says 77, route says 2

        Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request));
    }

    [Fact]
    public void NormalizeActivityRequest_ScopeMismatch_Returns400()
    {
        var request = ValidActivityRequest();
        request.Scope = "Mission";

        Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeActivityRequest(1, 2, request));
    }

    [Fact]
    public void NormalizeActivityRequest_MissingContextActivities_Returns400()
    {
        var request = ValidActivityRequest();
        request.Context!.Activities = null;

        Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeActivityRequest(1, 2, request));
    }

    [Fact]
    public void NormalizeActivityRequest_EmptyContextActivities_Returns400()
    {
        var request = ValidActivityRequest();
        request.Context!.Activities = [];

        Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeActivityRequest(1, 2, request));
    }

    // ── mission decision validation tests ─────────────────────────────────────

    [Fact]
    public void NormalizeMissionRequest_ContinueMission_WithRescueRequest_Returns400()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.ContinueMission);
        request.RescueRequest = new MissionRescueRequestDto { SupportTypes = ["rescue_support"] };

        var exception = Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request));

        Assert.Contains("continue_mission", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeMissionRequest_ContinueMission_WithHandover_Returns400()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.ContinueMission);
        request.Handover = new MissionHandoverDto { NeedsMissionTakeover = true };

        Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request));
    }

    [Fact]
    public void NormalizeMissionRequest_HandoverMission_MissingHandover_Returns400()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.HandoverMission);
        // no Handover block

        var exception = Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request));

        Assert.Contains("handover_mission", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeMissionRequest_HandoverMission_NeedsMissionTakeoverFalse_Returns400()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.HandoverMission);
        request.Handover = new MissionHandoverDto { NeedsMissionTakeover = false };

        Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request));
    }

    [Fact]
    public void NormalizeMissionRequest_HandoverMission_NeedsTakeover_Succeeds()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.HandoverMission);
        request.Handover = new MissionHandoverDto { NeedsMissionTakeover = true };

        var normalized = IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request);

        Assert.Equal(IncidentV2Constants.MissionDecisionCodes.HandoverMission, normalized.MissionDecision);
    }

    [Fact]
    public void NormalizeMissionRequest_RescueWholeTeam_MissingRescueRequest_Returns400()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.RescueWholeTeamImmediately);
        // no RescueRequest

        var exception = Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request));

        Assert.Contains("rescueRequest", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeMissionRequest_RescueWholeTeam_WithRescueRequest_Succeeds()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.RescueWholeTeamImmediately);
        request.IncidentType = "whole_team_stranded";
        request.RescueRequest = new MissionRescueRequestDto
        {
            SupportTypes = ["rescue_support"],
            Priority = "high",
            EvacuationPriority = "immediate"
        };
        request.TeamStatus = new MissionTeamStatusDto { TotalMembers = 4, SeverelyInjuredMembers = 1 };

        var normalized = IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request);

        Assert.True(normalized.NeedSupportSos);
        Assert.True(normalized.HasInjuredMember);
        Assert.Equal("whole_team_stranded", normalized.IncidentType);
        Assert.NotNull(normalized.SosContext);
        Assert.Equal("high", normalized.SosContext!.Priority);
        Assert.Equal("immediate", normalized.SosContext.EvacuationPriority);
    }

    // ── mission incidentType preservation ─────────────────────────────────────

    [Fact]
    public void NormalizeMissionRequest_PreservesSubtypeIncidentType()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.ContinueMission);
        request.IncidentType = "lost_communication";

        var normalized = IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request);

        Assert.Equal("lost_communication", normalized.IncidentType);
    }

    [Fact]
    public void NormalizeMissionRequest_NeedSupportSos_WhenRescueRequestPresent()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.PauseMission);
        request.RescueRequest = new MissionRescueRequestDto { SupportTypes = ["medical_support"] };

        var normalized = IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request);

        Assert.True(normalized.NeedSupportSos);
        Assert.NotNull(normalized.SosContext);
    }

    [Fact]
    public void NormalizeMissionRequest_NeedSupportSos_False_WhenNoRescueRequest()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.ContinueMission);

        var normalized = IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request);

        Assert.False(normalized.NeedSupportSos);
        Assert.Null(normalized.SosContext);
    }

    [Fact]
    public void NormalizeMissionRequest_HasInjuredMember_FromTeamStatus()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.PauseMission);
        request.RescueRequest = new MissionRescueRequestDto { SupportTypes = ["rescue_support"] };
        request.TeamStatus = new MissionTeamStatusDto { TotalMembers = 5, LightlyInjuredMembers = 2 };

        var normalized = IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request);

        Assert.True(normalized.HasInjuredMember);
    }

    [Fact]
    public void NormalizeMissionRequest_HasInjuredMember_FromUrgentMedical()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.PauseMission);
        request.RescueRequest = new MissionRescueRequestDto { SupportTypes = ["rescue_support"] };
        request.UrgentMedical = new UrgentMedicalDto { NeedsImmediateEmergencyCare = true };

        var normalized = IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request);

        Assert.True(normalized.HasInjuredMember);
    }

    [Fact]
    public void NormalizeMissionRequest_LocationFromContext()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.ContinueMission);
        request.Context!.Location = new GeoLocationDto { Latitude = 15.5, Longitude = 108.3 };

        var normalized = IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request);

        Assert.Equal(15.5, normalized.Latitude);
        Assert.Equal(108.3, normalized.Longitude);
    }

    [Fact]
    public void NormalizeMissionRequest_DescriptionFromNote()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.ContinueMission);
        request.Note = "Đường bị ngập.";

        var normalized = IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request);

        Assert.Equal("Đường bị ngập.", normalized.Summary);
    }

    [Fact]
    public void NormalizeMissionRequest_DetailJson_ContainsNestedContract()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.ContinueMission);
        request.IncidentType = "lost_communication";
        request.Context!.TeamName = "Alpha";

        var normalized = IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request);

        Assert.Contains("\"IncidentType\"", normalized.DetailJson);
        Assert.Contains("lost_communication", normalized.DetailJson);
        Assert.Contains("\"TeamName\"", normalized.DetailJson);
    }

    // ── activity normalization tests ───────────────────────────────────────────

    [Fact]
    public void NormalizeActivityRequest_AddsTakeoverSupport_WhenReassignRequested()
    {
        var request = ValidActivityRequest(needReassign: true, canContinue: true);
        request.Context!.Activities = [
            new ActivitySnapshotDto { ActivityId = 11 },
            new ActivitySnapshotDto { ActivityId = 13 }
        ];

        var normalized = IncidentV2NormalizationHelper.NormalizeActivityRequest(1, 2, request);

        Assert.True(normalized.NeedSupportSos);
        Assert.Equal(IncidentV2Constants.ActivityDecisionCodes.ReassignActivity, normalized.DecisionCode);
        Assert.NotNull(normalized.SosContext);
        Assert.Contains(
            IncidentV2Constants.SupportTypes.TakeoverActivity,
            normalized.SosContext!.SupportTypes,
            StringComparer.OrdinalIgnoreCase);
        Assert.False(normalized.ShouldFailSelectedActivities);
    }

    [Fact]
    public void NormalizeActivityRequest_DecisionCode_CannotContinue_WhenFalse()
    {
        var request = ValidActivityRequest(canContinue: false, needReassign: false);

        var normalized = IncidentV2NormalizationHelper.NormalizeActivityRequest(1, 2, request);

        Assert.Equal(IncidentV2Constants.ActivityDecisionCodes.CannotContinueActivity, normalized.DecisionCode);
        Assert.True(normalized.ShouldFailSelectedActivities);
    }

    [Fact]
    public void NormalizeActivityRequest_DecisionCode_ContinueActivity()
    {
        var request = ValidActivityRequest(canContinue: true, needReassign: false, needSos: false);

        var normalized = IncidentV2NormalizationHelper.NormalizeActivityRequest(1, 2, request);

        Assert.Equal(IncidentV2Constants.ActivityDecisionCodes.ContinueActivity, normalized.DecisionCode);
        Assert.False(normalized.ShouldFailSelectedActivities);
    }

    [Fact]
    public void NormalizeActivityRequest_NeedSupportSOS_False_WithSupportRequest_Returns400()
    {
        var request = ValidActivityRequest(canContinue: true, needReassign: false, needSos: false);
        request.SupportRequest = new ActivitySupportRequestDto { SupportTypes = ["medical_support"] };

        Assert.Throws<BadRequestException>(() =>
            IncidentV2NormalizationHelper.NormalizeActivityRequest(1, 2, request));
    }

    [Fact]
    public void NormalizeActivityRequest_FirstContextActivity_IsPrimaryActivityId()
    {
        var request = ValidActivityRequest();
        request.Context!.Activities = [
            new ActivitySnapshotDto { ActivityId = 77, Step = 3 },
            new ActivitySnapshotDto { ActivityId = 88, Step = 4 }
        ];

        var normalized = IncidentV2NormalizationHelper.NormalizeActivityRequest(1, 2, request);

        Assert.Equal(77, normalized.PrimaryActivityId);
        Assert.Contains(77, normalized.ActivityIds);
        Assert.Contains(88, normalized.ActivityIds);
    }

    [Fact]
    public void NormalizeActivityRequest_PreservesSubtypeIncidentType()
    {
        var request = ValidActivityRequest();
        request.IncidentType = "insufficient_staff";

        var normalized = IncidentV2NormalizationHelper.NormalizeActivityRequest(1, 2, request);

        Assert.Equal("insufficient_staff", normalized.IncidentType);
    }

    [Fact]
    public void NormalizeActivityRequest_HasInjuredMember_FromTeamStatus()
    {
        var request = ValidActivityRequest(canContinue: false);
        request.TeamStatus = new ActivityTeamStatusDto { TotalMembers = 5, LightlyInjuredMembers = 1 };

        var normalized = IncidentV2NormalizationHelper.NormalizeActivityRequest(1, 2, request);

        Assert.True(normalized.HasInjuredMember);
    }

    [Fact]
    public void NormalizeActivityRequest_SosContext_MeetupPoint_FromSupportRequest()
    {
        var request = ValidActivityRequest(needSos: true);
        request.SupportRequest = new ActivitySupportRequestDto
        {
            SupportTypes = ["vehicle_support"],
            MeetupPoint = "Cổng 3 khu công nghiệp"
        };

        var normalized = IncidentV2NormalizationHelper.NormalizeActivityRequest(1, 2, request);

        Assert.NotNull(normalized.SosContext);
        Assert.Equal("Cổng 3 khu công nghiệp", normalized.SosContext!.MeetupPoint);
    }

    [Fact]
    public void NormalizeActivityRequest_SosContext_AffectedResources_Propagated()
    {
        var request = ValidActivityRequest(needSos: true);
        request.AffectedResources = ["xuồng cứu sinh B12", "bộ đàm"];
        request.SupportRequest = new ActivitySupportRequestDto { SupportTypes = ["supply_support"] };

        var normalized = IncidentV2NormalizationHelper.NormalizeActivityRequest(1, 2, request);

        Assert.NotNull(normalized.SosContext?.AffectedResources);
        Assert.Contains("xuồng cứu sinh B12", normalized.SosContext!.AffectedResources!);
    }

    [Fact]
    public void NormalizeMissionRequest_SosContext_MedicalIssues_FromUrgentMedical()
    {
        var request = ValidMissionRequest(IncidentV2Constants.MissionDecisionCodes.RescueWholeTeamImmediately);
        request.RescueRequest = new MissionRescueRequestDto { SupportTypes = ["medical_support"] };
        request.UrgentMedical = new UrgentMedicalDto
        {
            NeedsImmediateEmergencyCare = true,
            EmergencyTypes = ["fracture", "shock"]
        };

        var normalized = IncidentV2NormalizationHelper.NormalizeMissionRequest(1, 2, request);

        Assert.NotNull(normalized.SosContext?.MedicalIssues);
        Assert.Contains("fracture", normalized.SosContext!.MedicalIssues!);
    }
}