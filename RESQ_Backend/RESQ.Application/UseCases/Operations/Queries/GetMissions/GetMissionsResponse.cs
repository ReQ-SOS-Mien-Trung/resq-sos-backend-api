using System.Text.Json;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissions;

public class GetMissionsResponse
{
    public List<MissionDto> Missions { get; set; } = [];
}

public class MissionDto
{
    public int Id { get; set; }
    public int? ClusterId { get; set; }
    public string? MissionType { get; set; }
    public double? PriorityScore { get; set; }
    public string? Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? ExpectedEndTime { get; set; }
    public bool? IsCompleted { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ActivityCount { get; set; }
    public List<MissionActivityDto> Activities { get; set; } = [];
    public List<AssignedTeamDto> Teams { get; set; } = [];

    // AI suggestion fields (most recent suggestion linked to this mission's cluster)
    public int? AiSuggestionId { get; set; }
    public string? SuggestedMissionTitle { get; set; }
    public string? ModelName { get; set; }
    public string? SuggestedMissionType { get; set; }
    public double? SuggestedPriorityScore { get; set; }
    public string? SuggestedSeverityLevel { get; set; }
    public double? AiConfidenceScore { get; set; }
    public string? OverallAssessment { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SpecialNotes { get; set; }
    public List<SuggestedActivityDto> SuggestedActivities { get; set; } = [];
    public List<SuggestedResourceDto> SuggestedResources { get; set; } = [];
    public DateTime? AiCreatedAt { get; set; }
}

public class MissionActivityDto
{
    public int Id { get; set; }
    public int? Step { get; set; }
    public string? ActivityCode { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public string? Target { get; set; }
    public string? Items { get; set; }
    /// <summary>Parsed supply list from Items JSON — matches SuggestedActivityDto.SuppliesToCollect.</summary>
    public List<SupplyToCollectDto>? SuppliesToCollect { get; set; }
    public double? TargetLatitude { get; set; }
    public double? TargetLongitude { get; set; }
    public string? Status { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? LastDecisionBy { get; set; }
}

/// <summary>AI suggestion metadata attached to a mission response — mirrors GenerateRescueMissionSuggestionResponse.</summary>
public class MissionAiSuggestionSection
{
    public int Id { get; set; }
    public string? SuggestedMissionTitle { get; set; }
    public string? ModelName { get; set; }
    public string? SuggestedMissionType { get; set; }
    public double? SuggestedPriorityScore { get; set; }
    public string? SuggestedSeverityLevel { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? OverallAssessment { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SpecialNotes { get; set; }
    public List<SuggestedActivityDto> SuggestedActivities { get; set; } = [];
    public List<SuggestedResourceDto> SuggestedResources { get; set; } = [];
    public DateTime? CreatedAt { get; set; }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    internal static MissionAiSuggestionSection? From(
        RESQ.Domain.Entities.Emergency.MissionAiSuggestionModel model)
    {
        if (model is null) return null;

        var section = new MissionAiSuggestionSection
        {
            Id = model.Id,
            SuggestedMissionTitle = model.SuggestedMissionTitle,
            ModelName = model.ModelName,
            SuggestedMissionType = model.SuggestedMissionType,
            SuggestedPriorityScore = model.SuggestedPriorityScore,
            SuggestedSeverityLevel = model.SuggestedSeverityLevel,
            ConfidenceScore = model.ConfidenceScore,
            CreatedAt = model.CreatedAt
        };

        // Parse Metadata JSON for extra fields
        if (!string.IsNullOrWhiteSpace(model.Metadata))
        {
            try
            {
                var meta = JsonSerializer.Deserialize<AiMetadata>(model.Metadata, JsonOpts);
                if (meta is not null)
                {
                    section.OverallAssessment = meta.OverallAssessment;
                    section.EstimatedDuration = meta.EstimatedDuration;
                    section.SpecialNotes = meta.SpecialNotes;
                    section.SuggestedResources = meta.SuggestedResources ?? [];
                }
            }
            catch { /* ignore malformed metadata */ }
        }

        // Parse SuggestedActivities from the first ActivityAiSuggestion blob
        var activityBlob = model.Activities.FirstOrDefault()?.SuggestedActivities;
        if (!string.IsNullOrWhiteSpace(activityBlob))
        {
            try
            {
                section.SuggestedActivities = JsonSerializer.Deserialize<List<SuggestedActivityDto>>(
                    activityBlob, JsonOpts) ?? [];
            }
            catch { /* ignore malformed activities */ }
        }

        return section;
    }

    private class AiMetadata
    {
        public string? OverallAssessment { get; set; }
        public string? EstimatedDuration { get; set; }
        public string? SpecialNotes { get; set; }
        public List<SuggestedResourceDto>? SuggestedResources { get; set; }
    }
}

/// <summary>Helper to parse Items jsonb → SuppliesToCollect for MissionActivityDto.</summary>
internal static class MissionActivityDtoHelper
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    internal static List<SupplyToCollectDto>? ParseSupplies(string? itemsJson)
    {
        if (string.IsNullOrWhiteSpace(itemsJson)) return null;
        try { return JsonSerializer.Deserialize<List<SupplyToCollectDto>>(itemsJson, JsonOpts); }
        catch { return null; }
    }
}

public class AssignedTeamDto
{
    public int MissionTeamId { get; set; }
    public int RescueTeamId { get; set; }
    public string? TeamName { get; set; }
    public string? TeamCode { get; set; }
    public string? TeamType { get; set; }
    public string? Status { get; set; }
    public string? Note { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
}
