using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;
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
        IncidentSosCreationContext? sosContext,
        IMediator mediator,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (sosContext is null)
        {
            return null;
        }

        if (sosContext.Latitude.HasValue != sosContext.Longitude.HasValue)
        {
            throw new BadRequestException("Latitude và Longitude c?a SOS context ph?i cùng có giá tr? ho?c cùng d? tr?ng.");
        }

        var latitude = sosContext.Latitude ?? incidentLatitude ?? missionTeam.Latitude;
        var longitude = sosContext.Longitude ?? incidentLongitude ?? missionTeam.Longitude;

        if (!latitude.HasValue || !longitude.HasValue)
        {
            throw new BadRequestException("Không xác d?nh du?c v? trí d? t?o support SOS cho incident.");
        }

        var reporter = missionTeam.RescueTeamMembers.FirstOrDefault(member => member.UserId == reportedBy);
        var rawMessage = string.IsNullOrWhiteSpace(sosContext.AdditionalDescription)
            ? incidentDescription.Trim()
            : sosContext.AdditionalDescription.Trim();

        var defaultAdultCount = sosContext.AdultCount ?? Math.Max(missionTeam.MemberCount, 1);
        var supportTypes = sosContext.SupportTypes
            .Select(type => type.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var sosType = ResolveSupportSosType(sosContext, supportTypes);

        var structuredDataJson = JsonSerializer.Serialize(new
        {
            incident = new
            {
                situation = "trapped",
                additional_description = rawMessage,
                has_injured = sosContext.HasInjured,
                people_count = new
                {
                    adult = defaultAdultCount,
                    child = 0,
                    elderly = 0
                },
                support_priority = sosContext.Priority,
                evacuation_priority = sosContext.EvacuationPriority,
                meetup_point = sosContext.MeetupPoint,
                affected_resources = sosContext.AffectedResources,
                medical_issues = sosContext.MedicalIssues
            },
            victims = BuildVictims(reporter, sosContext),
            team_incident_context = new
            {
                mission_id = missionId,
                mission_team_id = missionTeam.Id,
                mission_activity_id = activity?.Id,
                incident_scope = activity is null ? "Mission" : "Activity",
                incident_type = incidentType,
                reported_incident_type = sosContext.ReportedIncidentType,
                decision_code = decisionCode,
                team_name = missionTeam.TeamName,
                team_code = missionTeam.TeamCode,
                location_source = missionTeam.LocationSource,
                original_incident_description = incidentDescription.Trim()
            },
            operation_support = new
            {
                support_types = supportTypes,
                need_reassign_activity = supportTypes.Contains(
                    IncidentV2Constants.SupportTypes.TakeoverActivity,
                    StringComparer.OrdinalIgnoreCase),
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

    private static string ResolveSupportSosType(IncidentSosCreationContext sosContext, IReadOnlyCollection<string> supportTypes)
    {
        var hasMedicalNeed = sosContext.HasInjured
            || sosContext.MedicalIssues is { Count: > 0 }
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

    private static object[]? BuildVictims(MissionTeamMemberInfo? reporter, IncidentSosCreationContext sosContext)
    {
        var hasInjuredVictim = sosContext.HasInjured || sosContext.MedicalIssues is { Count: > 0 };
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
                    is_injured = sosContext.HasInjured,
                    severity = "moderate",
                    medical_issues = sosContext.MedicalIssues
                }
            }
        ];
    }
}
