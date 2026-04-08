using System.Text.Json;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class IncidentV2NormalizationHelper
{
    public static NormalizedMissionIncidentRequest NormalizeMissionRequest(MissionIncidentReportRequest request)
    {
        ValidateCoordinates(request.Latitude, request.Longitude);

        var missionDecision = NormalizeCode(request.MissionDecision, "MissionDecision");
        EnsureSupportedMissionDecision(missionDecision);
        var teamCondition = request.TeamCondition;
        var handover = request.Handover;
        var retreatCapability = NormalizeOptionalCode(teamCondition?.RetreatCapability);

        var supportRequest = CloneSupportRequest(request.SupportRequest);
        var needSupportSos = request.NeedSupportSos;
        var rescueRequired = missionDecision == IncidentV2Constants.MissionDecisionCodes.RescueWholeTeamImmediately
            || retreatCapability == IncidentV2Constants.RetreatCapabilityCodes.UrgentRescueNeeded;

        if (missionDecision == IncidentV2Constants.MissionDecisionCodes.ContinueMission && rescueRequired)
        {
            throw new BadRequestException("continue_mission không được dùng khi đội đang cần giải cứu khẩn cấp.");
        }

        if (!needSupportSos && supportRequest is not null)
        {
            throw new BadRequestException("NeedSupportSos = false nhưng vẫn có SupportRequest.");
        }

        if (missionDecision == IncidentV2Constants.MissionDecisionCodes.ContinueMission)
        {
            if (needSupportSos || supportRequest is not null)
            {
                throw new BadRequestException("continue_mission không được kèm support request.");
            }

            if (handover is not null)
            {
                throw new BadRequestException("continue_mission không được kèm handover block.");
            }
        }

        if (missionDecision == IncidentV2Constants.MissionDecisionCodes.HandoverMission && handover is null)
        {
            throw new BadRequestException("handover_mission bắt buộc phải có handover block.");
        }

        if (rescueRequired)
        {
            needSupportSos = true;
            if (supportRequest is null)
            {
                throw new BadRequestException("retreatCapability = urgent_rescue_needed hoặc rescue_whole_team_immediately bắt buộc phải có SupportRequest.");
            }
        }

        if (needSupportSos && supportRequest is null)
        {
            throw new BadRequestException("Thiếu SupportRequest dù incident yêu cầu hỗ trợ.");
        }

        if (supportRequest is not null)
        {
            supportRequest.SupportTypes = supportRequest.SupportTypes
                .Select(NormalizeOptionalCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ValidateCoordinates(supportRequest.Latitude, supportRequest.Longitude, "SupportRequest");
        }

        if (rescueRequired && supportRequest is { SupportTypes.Count: 0 })
        {
            supportRequest.SupportTypes.Add("rescue_support");
        }

        var summary = BuildMissionSummary(request, missionDecision, rescueRequired);
        var detailJson = JsonSerializer.Serialize(request);

        return new NormalizedMissionIncidentRequest(
            summary,
            request.Latitude,
            request.Longitude,
            missionDecision,
            needSupportSos,
            rescueRequired,
            teamCondition?.HasInjuredMember == true || supportRequest?.HasInjured == true,
            retreatCapability,
            handover,
            supportRequest,
            detailJson);
    }

    public static NormalizedActivityIncidentRequest NormalizeActivityRequest(ActivityIncidentReportRequest request)
    {
        ValidateCoordinates(request.Latitude, request.Longitude);

        if (request.ActivityIds is null || request.ActivityIds.Count == 0)
        {
            throw new BadRequestException("Activity incident phải chọn ít nhất một activity bị ảnh hưởng.");
        }

        var activityIds = request.ActivityIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (activityIds.Count == 0)
        {
            throw new BadRequestException("Danh sách activity bị ảnh hưởng không hợp lệ.");
        }

        if (request.PrimaryActivityId.HasValue && !activityIds.Contains(request.PrimaryActivityId.Value))
        {
            throw new BadRequestException("PrimaryActivityId phải nằm trong danh sách ActivityIds.");
        }

        var supportRequest = CloneSupportRequest(request.SupportRequest);
        var needSupportSos = request.NeedSupportSos;

        if (!needSupportSos && supportRequest is not null)
        {
            throw new BadRequestException("NeedSupportSos = false nhưng vẫn có SupportRequest.");
        }

        if (request.NeedReassignActivity)
        {
            needSupportSos = true;
            supportRequest ??= new IncidentSupportRequestData();
            supportRequest.SupportTypes ??= [];

            if (!supportRequest.SupportTypes.Any(type => string.Equals(type, IncidentV2Constants.SupportTypes.TakeoverActivity, StringComparison.OrdinalIgnoreCase)))
            {
                supportRequest.SupportTypes.Add(IncidentV2Constants.SupportTypes.TakeoverActivity);
            }
        }

        if (needSupportSos && supportRequest is null)
        {
            throw new BadRequestException("Thiếu SupportRequest dù activity incident yêu cầu hỗ trợ.");
        }

        if (supportRequest is not null)
        {
            supportRequest.SupportTypes = supportRequest.SupportTypes
                .Select(NormalizeOptionalCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ValidateCoordinates(supportRequest.Latitude, supportRequest.Longitude, "SupportRequest");
        }

        var decisionCode = request.NeedReassignActivity
            ? IncidentV2Constants.ActivityDecisionCodes.ReassignActivity
            : request.CanContinueActivity
                ? IncidentV2Constants.ActivityDecisionCodes.ContinueActivity
                : IncidentV2Constants.ActivityDecisionCodes.CannotContinueActivity;

        var hasWorkloadImpact = request.NeedReassignActivity || !request.CanContinueActivity || needSupportSos;
        var shouldFailSelectedActivities = !request.CanContinueActivity && !request.NeedReassignActivity;
        var summary = BuildActivitySummary(request, decisionCode);
        var detailJson = JsonSerializer.Serialize(request);

        return new NormalizedActivityIncidentRequest(
            summary,
            request.Latitude,
            request.Longitude,
            request.PrimaryActivityId ?? activityIds[0],
            activityIds,
            request.CanContinueActivity,
            request.NeedReassignActivity,
            needSupportSos,
            shouldFailSelectedActivities,
            hasWorkloadImpact,
            request.HasInjuredMember == true || supportRequest?.HasInjured == true,
            decisionCode,
            supportRequest,
            detailJson);
    }

    private static string BuildMissionSummary(MissionIncidentReportRequest request, string missionDecision, bool rescueRequired)
    {
        if (!string.IsNullOrWhiteSpace(request.Summary))
        {
            return request.Summary.Trim();
        }

        if (rescueRequired)
        {
            return "Đội cứu hộ yêu cầu giải cứu khẩn cấp.";
        }

        return missionDecision switch
        {
            IncidentV2Constants.MissionDecisionCodes.ContinueMission => "Đội cứu hộ báo mission incident nhưng vẫn tiếp tục nhiệm vụ.",
            IncidentV2Constants.MissionDecisionCodes.PauseMission => "Đội cứu hộ báo cần tạm dừng mission.",
            IncidentV2Constants.MissionDecisionCodes.StopMission => "Đội cứu hộ báo phải dừng mission.",
            IncidentV2Constants.MissionDecisionCodes.HandoverMission => "Đội cứu hộ báo cần bàn giao mission cho đội khác.",
            _ => "Đội cứu hộ báo mission incident."
        };
    }

    private static string BuildActivitySummary(ActivityIncidentReportRequest request, string decisionCode)
    {
        if (!string.IsNullOrWhiteSpace(request.Summary))
        {
            return request.Summary.Trim();
        }

        return decisionCode switch
        {
            IncidentV2Constants.ActivityDecisionCodes.ReassignActivity => "Đội cứu hộ báo activity incident và yêu cầu bàn giao activity.",
            IncidentV2Constants.ActivityDecisionCodes.CannotContinueActivity => "Đội cứu hộ báo activity incident khiến activity không thể tiếp tục.",
            _ => "Đội cứu hộ báo activity incident nhưng vẫn có thể tiếp tục xử lý."
        };
    }

    private static IncidentSupportRequestData? CloneSupportRequest(IncidentSupportRequestData? source)
    {
        if (source is null)
        {
            return null;
        }

        return new IncidentSupportRequestData
        {
            RawMessage = source.RawMessage,
            Latitude = source.Latitude,
            Longitude = source.Longitude,
            Situation = source.Situation,
            HasInjured = source.HasInjured,
            AdultCount = source.AdultCount,
            ChildCount = source.ChildCount,
            ElderlyCount = source.ElderlyCount,
            MedicalIssues = source.MedicalIssues?.ToList(),
            Address = source.Address,
            AdditionalDescription = source.AdditionalDescription,
            SupportTypes = source.SupportTypes?.ToList() ?? []
        };
    }

    private static void ValidateCoordinates(double? latitude, double? longitude, string path = "Incident")
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            throw new BadRequestException($"Latitude và Longitude của {path} phải cùng có giá trị hoặc cùng để trống.");
        }
    }

    private static string NormalizeCode(string? value, string fieldName)
    {
        var normalized = NormalizeOptionalCode(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BadRequestException($"{fieldName} không được để trống.");
        }

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

    private static string? NormalizeOptionalCode(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : value.Trim().ToLowerInvariant();
}

internal sealed record NormalizedMissionIncidentRequest(
    string Summary,
    double? Latitude,
    double? Longitude,
    string MissionDecision,
    bool NeedSupportSos,
    bool RescueRequired,
    bool HasInjuredMember,
    string? RetreatCapability,
    MissionIncidentHandoverDto? Handover,
    IncidentSupportRequestData? SupportRequest,
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
    string DecisionCode,
    IncidentSupportRequestData? SupportRequest,
    string DetailJson);