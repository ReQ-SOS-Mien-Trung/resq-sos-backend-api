using System.Text.Json;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.Shared;

public class TeamIncidentQueryDto
{
    public int IncidentId { get; set; }
    public int MissionTeamId { get; set; }
    public int? MissionActivityId { get; set; }
    public string IncidentScope { get; set; } = "Mission";
    public string IncidentType { get; set; } = string.Empty;
    public string? DecisionCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public ReportedByDto? ReportedBy { get; set; }
    public DateTime? ReportedAt { get; set; }
    public bool HasInjuredMember { get; set; }
    public bool HasSupportRequest { get; set; }
    public int? SupportSosRequestId { get; set; }
    public List<IncidentAffectedActivityDto> AffectedActivities { get; set; } = [];
    public JsonElement? Detail { get; set; }
}

internal static class TeamIncidentQueryDtoMapper
{
    public static TeamIncidentQueryDto ToDto(TeamIncidentModel incident, ReportedByDto? reportedBy) => new()
    {
        IncidentId = incident.Id,
        MissionTeamId = incident.MissionTeamId,
        MissionActivityId = incident.MissionActivityId,
        IncidentScope = incident.IncidentScope.ToString(),
        IncidentType = string.IsNullOrWhiteSpace(incident.IncidentType)
            ? incident.IncidentScope == TeamIncidentScope.Activity
                ? IncidentV2Constants.ActivityIncidentType
                : IncidentV2Constants.MissionIncidentType
            : incident.IncidentType,
        DecisionCode = incident.DecisionCode,
        Latitude = incident.Latitude,
        Longitude = incident.Longitude,
        Description = incident.Description,
        Status = incident.Status.ToString(),
        ReportedBy = reportedBy,
        ReportedAt = incident.ReportedAt,
        HasInjuredMember = HasInjuredMember(incident.DetailJson),
        HasSupportRequest = incident.NeedSupportSos || incident.SupportSosRequestId.HasValue,
        SupportSosRequestId = incident.SupportSosRequestId,
        AffectedActivities = incident.AffectedActivities
            .OrderBy(activity => activity.OrderIndex)
            .Select(activity => new IncidentAffectedActivityDto
            {
                MissionActivityId = activity.MissionActivityId,
                OrderIndex = activity.OrderIndex,
                IsPrimary = activity.IsPrimary,
                Step = activity.Step,
                ActivityType = activity.ActivityType,
                Status = activity.Status?.ToString()
            })
            .ToList(),
        Detail = ParseDetail(incident.DetailJson)
    };

    private static JsonElement? ParseDetail(string? detailJson)
    {
        if (string.IsNullOrWhiteSpace(detailJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(detailJson);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasInjuredMember(string? detailJson)
    {
        var detail = ParseDetail(detailJson);
        if (!detail.HasValue)
        {
            return false;
        }

        return ContainsTrue(detail.Value, "hasInjuredMember")
            || ContainsTrue(detail.Value, "hasInjured")
            || ContainsNonEmptyArray(detail.Value, "medicalIssues");
    }

    private static bool ContainsTrue(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                if (ContainsTrue(property.Value, propertyName))
                {
                    return true;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsTrue(item, propertyName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsNonEmptyArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.Array
                    && property.Value.GetArrayLength() > 0)
                {
                    return true;
                }

                if (ContainsNonEmptyArray(property.Value, propertyName))
                {
                    return true;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsNonEmptyArray(item, propertyName))
                {
                    return true;
                }
            }
        }

        return false;
    }
}