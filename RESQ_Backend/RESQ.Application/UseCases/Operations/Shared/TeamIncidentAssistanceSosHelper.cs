using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;
using RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class TeamIncidentAssistanceSosHelper
{
    public static async Task<CreateSosRequestResponse?> CreateSupportSosAsync(
        int missionId,
        MissionTeamModel missionTeam,
        MissionActivityModel? activity,
        Guid reportedBy,
        string incidentType,
        string? decisionCode,
        string incidentDescription,
        double? incidentLatitude,
        double? incidentLongitude,
        bool needSupportSos,
        bool needReassignActivity,
        IncidentSupportRequestData? supportRequest,
        IMediator mediator,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!needSupportSos)
        {
            return null;
        }

        if (supportRequest is null)
        {
            throw new BadRequestException("Thiếu thông tin SupportRequest dù incident yêu cầu hỗ trợ.");
        }

        if (supportRequest.Latitude.HasValue != supportRequest.Longitude.HasValue)
        {
            throw new BadRequestException("Latitude và Longitude của SupportRequest phải cùng có giá trị hoặc cùng để trống.");
        }

        var latitude = supportRequest.Latitude ?? incidentLatitude ?? missionTeam.Latitude;
        var longitude = supportRequest.Longitude ?? incidentLongitude ?? missionTeam.Longitude;

        if (!latitude.HasValue || !longitude.HasValue)
        {
            throw new BadRequestException("Không xác định được vị trí để tạo support SOS cho incident.");
        }

        var reporter = missionTeam.RescueTeamMembers.FirstOrDefault(member => member.UserId == reportedBy);
        var rawMessage = string.IsNullOrWhiteSpace(supportRequest.RawMessage)
            ? incidentDescription.Trim()
            : supportRequest.RawMessage.Trim();

        var defaultAdultCount = supportRequest.AdultCount ?? Math.Max(missionTeam.MemberCount, 1);
        var supportTypes = supportRequest.SupportTypes
            .Select(type => type.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var sosType = ResolveSupportSosType(supportRequest, supportTypes);

        var structuredDataJson = JsonSerializer.Serialize(new
        {
            incident = new
            {
                situation = string.IsNullOrWhiteSpace(supportRequest.Situation) ? "trapped" : supportRequest.Situation,
                address = supportRequest.Address,
                additional_description = string.IsNullOrWhiteSpace(supportRequest.AdditionalDescription)
                    ? incidentDescription.Trim()
                    : supportRequest.AdditionalDescription.Trim(),
                has_injured = supportRequest.HasInjured,
                people_count = new
                {
                    adult = defaultAdultCount,
                    child = supportRequest.ChildCount ?? 0,
                    elderly = supportRequest.ElderlyCount ?? 0
                }
            },
            victims = BuildVictims(reporter, supportRequest),
            team_incident_context = new
            {
                mission_id = missionId,
                mission_team_id = missionTeam.Id,
                mission_activity_id = activity?.Id,
                incident_scope = activity is null ? "Mission" : "Activity",
                incident_type = incidentType,
                decision_code = decisionCode,
                team_name = missionTeam.TeamName,
                team_code = missionTeam.TeamCode,
                location_source = missionTeam.LocationSource,
                original_incident_description = incidentDescription.Trim()
            },
            operation_support = new
            {
                support_types = supportTypes,
                need_reassign_activity = needReassignActivity,
                requested_sos_type = sosType,
                origin = "rescuer_incident"
            }
        });

        var reporterInfoJson = JsonSerializer.Serialize(new
        {
            user_id = reportedBy,
            user_name = reporter?.FullName,
            user_phone = reporter?.Phone,
            is_online = true
        });

        var victimInfoJson = JsonSerializer.Serialize(new
        {
            user_id = reportedBy,
            user_name = reporter?.FullName ?? missionTeam.TeamName,
            user_phone = reporter?.Phone
        });

        var response = await mediator.Send(
            new CreateSosRequestCommand(
                reportedBy,
                new GeoLocation(latitude.Value, longitude.Value),
                rawMessage,
                SosType: sosType,
                StructuredData: structuredDataJson,
                VictimInfo: victimInfoJson,
                ReporterInfo: reporterInfoJson),
            cancellationToken);

        logger.LogInformation(
            "Created support SOS #{SosRequestId} for MissionTeamId={MissionTeamId} MissionId={MissionId} IncidentActivityId={ActivityId} IncidentType={IncidentType} DecisionCode={DecisionCode}",
            response.Id,
            missionTeam.Id,
            missionId,
            activity?.Id,
            incidentType,
            decisionCode);

        return response;
    }

    private static string ResolveSupportSosType(IncidentSupportRequestData supportRequest, IReadOnlyCollection<string> supportTypes)
    {
        var hasMedicalNeed = supportRequest.HasInjured == true
            || supportRequest.MedicalIssues is { Count: > 0 }
            || supportTypes.Any(type => type.Contains("medical", StringComparison.OrdinalIgnoreCase));

        if (hasMedicalNeed)
        {
            return "MEDICAL";
        }

        var supplyOnly = supportTypes.Count > 0 && supportTypes.All(type =>
            string.Equals(type, IncidentV2Constants.SupportTypes.SupplySupport, StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, IncidentV2Constants.SupportTypes.VehicleSupport, StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, IncidentV2Constants.SupportTypes.FuelSupport, StringComparison.OrdinalIgnoreCase));

        return supplyOnly ? "SUPPLY" : "RESCUE";
    }

    private static object[]? BuildVictims(MissionTeamMemberInfo? reporter, IncidentSupportRequestData supportRequest)
    {
        var hasInjuredVictim = supportRequest.HasInjured == true || supportRequest.MedicalIssues is { Count: > 0 };
        if (!hasInjuredVictim)
        {
            return null;
        }

        return
        [
            new
            {
                person_type = "adult",
                custom_name = reporter?.FullName,
                person_phone = reporter?.Phone,
                incident_status = new
                {
                    is_injured = supportRequest.HasInjured ?? true,
                    severity = "moderate",
                    medical_issues = supportRequest.MedicalIssues
                }
            }
        ];
    }
}