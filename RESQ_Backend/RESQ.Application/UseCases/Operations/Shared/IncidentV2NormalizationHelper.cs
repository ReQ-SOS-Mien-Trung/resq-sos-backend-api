using System.Text.Json;
using RESQ.Application.Exceptions;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class IncidentV2NormalizationHelper
{
        public static NormalizedMissionIncidentRequest NormalizeMissionRequest(
        int missionId,
        int missionTeamId,
        MissionIncidentReportRequest request)
    {
        // scope / context validation
        if (!string.Equals(request.Scope, "Mission", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("scope phải là 'Mission' cho endpoint báo mission incident.");

        if (request.Context?.MissionId is not null && request.Context.MissionId != missionId)
            throw new BadRequestException("context.missionId không khớp với missionId trên route.");

        if (request.Context?.MissionTeamId is not null && request.Context.MissionTeamId != missionTeamId)
            throw new BadRequestException("context.missionTeamId không khớp với missionTeamId trên route.");

        // location
        var latitude = request.Context?.Location?.Latitude;
        var longitude = request.Context?.Location?.Longitude;
        ValidateCoordinates(latitude, longitude);

        // missionDecision
        var missionDecision = NormalizeCode(request.MissionDecision, "MissionDecision");
        EnsureSupportedMissionDecision(missionDecision);

        // decision-specific rules
        var isRescue = missionDecision == IncidentV2Constants.MissionDecisionCodes.RescueWholeTeamImmediately;

        if (missionDecision == IncidentV2Constants.MissionDecisionCodes.ContinueMission)
        {
            if (request.RescueRequest is not null)
                throw new BadRequestException("continue_mission không được kèm rescueRequest.");

            if (request.Handover is not null)
                throw new BadRequestException("continue_mission không được kèm handover block.");
        }

        if (missionDecision == IncidentV2Constants.MissionDecisionCodes.HandoverMission)
        {
            if (request.Handover is null)
                throw new BadRequestException("handover_mission bắt buộc phải có handover block.");

            if (!request.Handover.NeedsMissionTakeover)
                throw new BadRequestException("handover_mission bắt buộc handover.needsMissionTakeover = true.");
        }

        if (isRescue && request.RescueRequest is null)
            throw new BadRequestException("rescue_whole_team_immediately bắt buộc phải có rescueRequest.");

        // derived flags
        var needSupportSos = request.RescueRequest is not null;

        var teamStatus = request.TeamStatus;
        var urgentMedical = request.UrgentMedical;
        var hasInjuredMember =
            urgentMedical?.NeedsImmediateEmergencyCare == true
            || (teamStatus?.LightlyInjuredMembers ?? 0) > 0
            || (teamStatus?.SeverelyInjuredMembers ?? 0) > 0
            || (teamStatus?.ImmobileMembers ?? 0) > 0;

        // build SOS context from rescueRequest
        IncidentSosCreationContext? sosContext = null;
        if (needSupportSos && request.RescueRequest is not null)
        {
            var supportTypes = (request.RescueRequest.SupportTypes ?? [])
                .Select(type => type.Trim().ToLowerInvariant())
                .Where(type => !string.IsNullOrWhiteSpace(type))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (isRescue && supportTypes.Count == 0)
                supportTypes.Add("rescue_support");

            var civilianCount = request.Context?.CiviliansWithTeam?.CivilianCount ?? 0;
            var adultCount = (teamStatus?.TotalMembers ?? 0) + civilianCount;

            sosContext = new IncidentSosCreationContext(
                SupportTypes: supportTypes,
                Priority: request.RescueRequest.Priority,
                EvacuationPriority: request.RescueRequest.EvacuationPriority,
                MeetupPoint: null,
                HasInjured: hasInjuredMember,
                MedicalIssues: urgentMedical?.EmergencyTypes,
                AdultCount: adultCount > 0 ? adultCount : null,
                AffectedResources: null,
                AdditionalDescription: request.Note,
                Latitude: latitude,
                Longitude: longitude,
                ReportedIncidentType: request.IncidentType);
        }

        // description
        var description = !string.IsNullOrWhiteSpace(request.Note)
            ? request.Note.Trim()
            : BuildMissionDescription(missionDecision, request.IncidentType, isRescue);

        var detailJson = JsonSerializer.Serialize(request);

        return new NormalizedMissionIncidentRequest(
            Summary: description,
            Latitude: latitude,
            Longitude: longitude,
            MissionDecision: missionDecision,
            NeedSupportSos: needSupportSos,
            HasInjuredMember: hasInjuredMember,
            IncidentType: NormalizeOptionalCode(request.IncidentType),
            SosContext: sosContext,
            DetailJson: detailJson);
    }

    public static NormalizedActivityIncidentRequest NormalizeActivityRequest(
        int missionId,
        int missionTeamId,
        ActivityIncidentReportRequest request)
    {
        // scope / context validation
        if (!string.Equals(request.Scope, "Activity", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("scope phải là 'Activity' cho endpoint báo activity incident.");

        if (request.Context?.MissionId is not null && request.Context.MissionId != missionId)
            throw new BadRequestException("context.missionId không khớp với missionId trên route.");

        if (request.Context?.MissionTeamId is not null && request.Context.MissionTeamId != missionTeamId)
            throw new BadRequestException("context.missionTeamId không khớp với missionTeamId trên route.");

        // activities list
        var activitySnapshots = request.Context?.Activities;
        if (activitySnapshots is null || activitySnapshots.Count == 0)
            throw new BadRequestException("activity incident phải cung cấp context.activities với ít nhất một activity.");

        var activityIds = activitySnapshots
            .Where(snapshot => snapshot.ActivityId > 0)
            .Select(snapshot => snapshot.ActivityId)
            .Distinct()
            .ToList();

        if (activityIds.Count == 0)
            throw new BadRequestException("Danh sách context.activities không có activityId hợp lệ.");

        var primaryActivityId = activityIds[0];

        // location
        var latitude = request.Context?.Location?.Latitude;
        var longitude = request.Context?.Location?.Longitude;
        ValidateCoordinates(latitude, longitude);

        // impact
        var impact = request.Impact ?? new ActivityImpactDto();
        var canContinue = impact.CanContinueActivity;
        var needReassign = impact.NeedReassignActivity;
        var impactNeedSos = impact.NeedSupportSOS;

        var needSupportSos = impactNeedSos || request.SupportRequest is not null || needReassign;

        // 400: explicit false + supportRequest present (not auto-sos from reassign)
        if (!impactNeedSos && !needReassign && request.SupportRequest is not null)
            throw new BadRequestException(
                "impact.needSupportSOS = false nhưng vẫn có supportRequest. Đặt needSupportSOS = true hoặc bỏ supportRequest.");

        // decision code derived from impact
        var decisionCode = needReassign
            ? IncidentV2Constants.ActivityDecisionCodes.ReassignActivity
            : canContinue
                ? IncidentV2Constants.ActivityDecisionCodes.ContinueActivity
                : IncidentV2Constants.ActivityDecisionCodes.CannotContinueActivity;

        var hasWorkloadImpact = needReassign || !canContinue || needSupportSos;
        var shouldFailSelectedActivities = !canContinue && !needReassign;

        var teamStatus = request.TeamStatus;
        var hasInjuredMember = (teamStatus?.LightlyInjuredMembers ?? 0) > 0;

        // build SOS context from supportRequest + auto-reassign injection
        IncidentSosCreationContext? sosContext = null;
        if (needSupportSos)
        {
            var supportTypes = (request.SupportRequest?.SupportTypes ?? [])
                .Select(type => type.Trim().ToLowerInvariant())
                .Where(type => !string.IsNullOrWhiteSpace(type))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (needReassign
                && !supportTypes.Contains(IncidentV2Constants.SupportTypes.TakeoverActivity, StringComparer.OrdinalIgnoreCase))
            {
                supportTypes.Add(IncidentV2Constants.SupportTypes.TakeoverActivity);
            }

            sosContext = new IncidentSosCreationContext(
                SupportTypes: supportTypes,
                Priority: request.SupportRequest?.Priority,
                EvacuationPriority: null,
                MeetupPoint: request.SupportRequest?.MeetupPoint,
                HasInjured: hasInjuredMember,
                MedicalIssues: null,
                AdultCount: teamStatus?.TotalMembers ?? request.SupportRequest?.Counts?.PeopleCount,
                AffectedResources: request.AffectedResources,
                AdditionalDescription: request.Note,
                Latitude: latitude,
                Longitude: longitude,
                ReportedIncidentType: request.IncidentType);
        }

        var description = !string.IsNullOrWhiteSpace(request.Note)
            ? request.Note.Trim()
            : BuildActivityDescription(decisionCode, request.IncidentType);

        var detailJson = JsonSerializer.Serialize(request);

        return new NormalizedActivityIncidentRequest(
            Summary: description,
            Latitude: latitude,
            Longitude: longitude,
            PrimaryActivityId: primaryActivityId,
            ActivityIds: activityIds,
            CanContinueActivity: canContinue,
            NeedReassignActivity: needReassign,
            NeedSupportSos: needSupportSos,
            ShouldFailSelectedActivities: shouldFailSelectedActivities,
            HasWorkloadImpact: hasWorkloadImpact,
            HasInjuredMember: hasInjuredMember,
            IncidentType: NormalizeOptionalCode(request.IncidentType),
            DecisionCode: decisionCode,
            SosContext: sosContext,
            DetailJson: detailJson);
    }

    private static string BuildMissionDescription(string missionDecision, string? incidentType, bool isRescue)
    {
        if (isRescue)
            return "Đội cứu hộ yêu cầu giải cứu khẩn cấp.";

        if (!string.IsNullOrWhiteSpace(incidentType))
            return $"Incident: {incidentType} — quyết định: {missionDecision}.";

        return missionDecision switch
        {
            IncidentV2Constants.MissionDecisionCodes.ContinueMission => "Đội cứu hộ báo mission incident nhưng vẫn tiếp tục nhiệm vụ.",
            IncidentV2Constants.MissionDecisionCodes.PauseMission => "Đội cứu hộ báo cần tạm dừng mission.",
            IncidentV2Constants.MissionDecisionCodes.StopMission => "Đội cứu hộ báo phải dừng mission.",
            IncidentV2Constants.MissionDecisionCodes.HandoverMission => "Đội cứu hộ báo cần bàn giao mission cho đội khác.",
            _ => "Đội cứu hộ báo mission incident."
        };
    }

    private static string BuildActivityDescription(string decisionCode, string? incidentType)
    {
        var prefix = !string.IsNullOrWhiteSpace(incidentType) ? $"Incident: {incidentType} – " : string.Empty;
        return decisionCode switch
        {
            IncidentV2Constants.ActivityDecisionCodes.ReassignActivity =>
                $"{prefix}yêu cầu bàn giao activity cho đội khác.",
            IncidentV2Constants.ActivityDecisionCodes.CannotContinueActivity =>
                $"{prefix}activity không thể tiếp tục.",
            _ =>
                $"{prefix}activity vẫn có thể tiếp tục xử lý."
        };
    }

    private static void ValidateCoordinates(double? latitude, double? longitude, string path = "context.location")
    {
        if (latitude.HasValue != longitude.HasValue)
            throw new BadRequestException(
                $"Latitude và Longitude của {path} phải cùng có giá trị hoặc cùng để trống.");
    }

    private static string NormalizeCode(string? value, string fieldName)
    {
        var normalized = NormalizeOptionalCode(value);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new BadRequestException($"{fieldName} không được để trống.");

        return normalized;
    }

    private static void EnsureSupportedMissionDecision(string missionDecision)
    {
        if (missionDecision is IncidentV2Constants.MissionDecisionCodes.ContinueMission
            or IncidentV2Constants.MissionDecisionCodes.PauseMission
            or IncidentV2Constants.MissionDecisionCodes.StopMission
            or IncidentV2Constants.MissionDecisionCodes.HandoverMission
            or IncidentV2Constants.MissionDecisionCodes.RescueWholeTeamImmediately)
        {
            return;
        }

        throw new BadRequestException($"MissionDecision không hợp lệ: {missionDecision}.");
    }

    private static string? NormalizeOptionalCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}

internal sealed record NormalizedMissionIncidentRequest(
    string Summary,
    double? Latitude,
    double? Longitude,
    string MissionDecision,
    bool NeedSupportSos,
    bool HasInjuredMember,
    string? IncidentType,
    IncidentSosCreationContext? SosContext,
    string DetailJson);

internal sealed record NormalizedActivityIncidentRequest(
    string Summary,
    double? Latitude,
    double? Longitude,
    int PrimaryActivityId,
    IReadOnlyCollection<int> ActivityIds,
    bool CanContinueActivity,
    bool NeedReassignActivity,
    bool NeedSupportSos,
    bool ShouldFailSelectedActivities,
    bool HasWorkloadImpact,
    bool HasInjuredMember,
    string? IncidentType,
    string DecisionCode,
    IncidentSosCreationContext? SosContext,
    string DetailJson);
