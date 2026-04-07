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
    public static async Task<CreateSosRequestResponse?> CreateAssistanceSosAsync(
        int missionId,
        MissionTeamModel missionTeam,
        MissionActivityModel? activity,
        Guid reportedBy,
        string incidentDescription,
        double? incidentLatitude,
        double? incidentLongitude,
        bool needsRescueAssistance,
        IncidentAssistanceSosRequestData? assistanceSos,
        IMediator mediator,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!needsRescueAssistance)
        {
            return null;
        }

        if (assistanceSos is null)
        {
            throw new BadRequestException("Thiếu thông tin SOS hỗ trợ dù đã chọn cần đội khác giải cứu.");
        }

        if (assistanceSos.Latitude.HasValue != assistanceSos.Longitude.HasValue)
        {
            throw new BadRequestException("Latitude và Longitude của SOS hỗ trợ phải cùng có giá trị hoặc cùng để trống.");
        }

        var latitude = assistanceSos.Latitude ?? incidentLatitude ?? missionTeam.Latitude;
        var longitude = assistanceSos.Longitude ?? incidentLongitude ?? missionTeam.Longitude;

        if (!latitude.HasValue || !longitude.HasValue)
        {
            throw new BadRequestException("Không xác định được vị trí để tạo SOS hỗ trợ cho đội cứu hộ.");
        }

        var reporter = missionTeam.RescueTeamMembers.FirstOrDefault(member => member.UserId == reportedBy);
        var rawMessage = string.IsNullOrWhiteSpace(assistanceSos.RawMessage)
            ? incidentDescription.Trim()
            : assistanceSos.RawMessage.Trim();

        var defaultAdultCount = assistanceSos.AdultCount ?? Math.Max(missionTeam.MemberCount, 1);
        var structuredDataJson = JsonSerializer.Serialize(new
        {
            incident = new
            {
                situation = string.IsNullOrWhiteSpace(assistanceSos.Situation) ? "trapped" : assistanceSos.Situation,
                address = assistanceSos.Address,
                additional_description = string.IsNullOrWhiteSpace(assistanceSos.AdditionalDescription)
                    ? incidentDescription.Trim()
                    : assistanceSos.AdditionalDescription.Trim(),
                has_injured = assistanceSos.HasInjured,
                people_count = new
                {
                    adult = defaultAdultCount,
                    child = assistanceSos.ChildCount ?? 0,
                    elderly = assistanceSos.ElderlyCount ?? 0
                }
            },
            victims = BuildVictims(reporter, assistanceSos),
            team_incident_context = new
            {
                mission_id = missionId,
                mission_team_id = missionTeam.Id,
                mission_activity_id = activity?.Id,
                incident_scope = activity is null ? "Mission" : "Activity",
                team_name = missionTeam.TeamName,
                team_code = missionTeam.TeamCode,
                location_source = missionTeam.LocationSource,
                original_incident_description = incidentDescription.Trim()
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
                SosType: string.IsNullOrWhiteSpace(assistanceSos.SosType) ? "RESCUE" : assistanceSos.SosType.Trim(),
                StructuredData: structuredDataJson,
                VictimInfo: victimInfoJson,
                ReporterInfo: reporterInfoJson),
            cancellationToken);

        logger.LogInformation(
            "Created assistance SOS #{SosRequestId} for MissionTeamId={MissionTeamId} MissionId={MissionId} IncidentActivityId={ActivityId}",
            response.Id,
            missionTeam.Id,
            missionId,
            activity?.Id);

        return response;
    }

    private static object[]? BuildVictims(MissionTeamMemberInfo? reporter, IncidentAssistanceSosRequestData assistanceSos)
    {
        var hasInjuredVictim = assistanceSos.HasInjured == true || assistanceSos.MedicalIssues is { Count: > 0 };
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
                    is_injured = assistanceSos.HasInjured ?? true,
                    severity = "moderate",
                    medical_issues = assistanceSos.MedicalIssues
                }
            }
        ];
    }
}