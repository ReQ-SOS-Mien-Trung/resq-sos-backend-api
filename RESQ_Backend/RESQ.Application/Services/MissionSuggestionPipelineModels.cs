namespace RESQ.Application.Services;

public class MissionRequiredSupplyFragment
{
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Category { get; set; }
    public string? Notes { get; set; }
}

public class MissionRequiredTeamNeedFragment
{
    public string? TeamType { get; set; }
    public int Quantity { get; set; }
    public string? Reason { get; set; }
}

public class MissionSosRequirementFragment
{
    public int SosRequestId { get; set; }
    public string? Summary { get; set; }
    public string? Priority { get; set; }
    public bool? NeedsImmediateSafeTransfer { get; set; }
    public bool? UrgentRescueRequiresImmediateSafeTransfer { get; set; }
    public bool? CanWaitForCombinedMission { get; set; }
    public bool? RequiresSupplyBeforeRescue { get; set; }
    public string? HandlingReason { get; set; }
    public List<MissionRequiredSupplyFragment> RequiredSupplies { get; set; } = [];
    public List<MissionRequiredTeamNeedFragment> RequiredTeams { get; set; } = [];
}

public class MissionRequirementsFragment
{
    public string? SuggestedMissionTitle { get; set; }
    public string? SuggestedMissionType { get; set; }
    public double? SuggestedPriorityScore { get; set; }
    public string? SuggestedSeverityLevel { get; set; }
    public string? OverallAssessment { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SpecialNotes { get; set; }
    public bool NeedsAdditionalDepot { get; set; }
    public bool SplitClusterRecommended { get; set; }
    public string? SplitClusterReason { get; set; }
    public List<SupplyShortageDto> SupplyShortages { get; set; } = [];
    public double ConfidenceScore { get; set; }
    public List<SuggestedResourceDto> SuggestedResources { get; set; } = [];
    public List<MissionSosRequirementFragment> SosRequirements { get; set; } = [];
}

public class MissionActivityFragment
{
    public string ActivityKey { get; set; } = string.Empty;
    public int Step { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Priority { get; set; }
    public string? EstimatedTime { get; set; }
    public string? ExecutionMode { get; set; }
    public int? RequiredTeamCount { get; set; }
    public string? CoordinationGroupKey { get; set; }
    public string? CoordinationNotes { get; set; }
    public int? SosRequestId { get; set; }
    public int? DepotId { get; set; }
    public string? DepotName { get; set; }
    public string? DepotAddress { get; set; }
    public double? DepotLatitude { get; set; }
    public double? DepotLongitude { get; set; }
    public int? AssemblyPointId { get; set; }
    public string? AssemblyPointName { get; set; }
    public double? AssemblyPointLatitude { get; set; }
    public double? AssemblyPointLongitude { get; set; }
    public List<SupplyToCollectDto>? SuppliesToCollect { get; set; }
    public SuggestedTeamDto? SuggestedTeam { get; set; }
}

public class MissionDepotFragment
{
    public List<MissionActivityFragment> Activities { get; set; } = [];
    public string? SpecialNotes { get; set; }
    public bool NeedsAdditionalDepot { get; set; }
    public List<SupplyShortageDto> SupplyShortages { get; set; } = [];
    public double ConfidenceScore { get; set; }
}

public class MissionActivityAssignmentFragment
{
    public string ActivityKey { get; set; } = string.Empty;
    public string? ExecutionMode { get; set; }
    public int? RequiredTeamCount { get; set; }
    public string? CoordinationGroupKey { get; set; }
    public string? CoordinationNotes { get; set; }
    public SuggestedTeamDto? SuggestedTeam { get; set; }
}

public class MissionTeamFragment
{
    public List<MissionActivityAssignmentFragment> ActivityAssignments { get; set; } = [];
    public List<MissionActivityFragment> AdditionalActivities { get; set; } = [];
    public List<string> OrderedActivityKeys { get; set; } = [];
    public SuggestedTeamDto? SuggestedTeam { get; set; }
    public string? SpecialNotes { get; set; }
    public double ConfidenceScore { get; set; }
}

public class MissionDraftBody
{
    public string? MissionTitle { get; set; }
    public string? MissionType { get; set; }
    public double? PriorityScore { get; set; }
    public string? SeverityLevel { get; set; }
    public string? OverallAssessment { get; set; }
    public List<MissionDraftActivityDto> Activities { get; set; } = [];
    public List<SuggestedResourceDto> Resources { get; set; } = [];
    public SuggestedTeamDto? SuggestedTeam { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SpecialNotes { get; set; }
    public bool NeedsAdditionalDepot { get; set; }
    public List<SupplyShortageDto> SupplyShortages { get; set; } = [];
    public double ConfidenceScore { get; set; }
}

public class MissionDraftActivityDto
{
    public int Step { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public string? EstimatedTime { get; set; }
    public string? ExecutionMode { get; set; }
    public int? RequiredTeamCount { get; set; }
    public string? CoordinationGroupKey { get; set; }
    public string? CoordinationNotes { get; set; }
    public int? SosRequestId { get; set; }
    public int? DepotId { get; set; }
    public string? DepotName { get; set; }
    public string? DepotAddress { get; set; }
    public double? DepotLatitude { get; set; }
    public double? DepotLongitude { get; set; }
    public int? AssemblyPointId { get; set; }
    public string? AssemblyPointName { get; set; }
    public double? AssemblyPointLatitude { get; set; }
    public double? AssemblyPointLongitude { get; set; }
    public List<SupplyToCollectDto>? SuppliesToCollect { get; set; }
    public SuggestedTeamDto? SuggestedTeam { get; set; }
}

public class MissionSuggestionStageSnapshot
{
    public string Status { get; set; } = "pending";
    public string? PromptType { get; set; }
    public string? ModelName { get; set; }
    public long? ResponseTimeMs { get; set; }
    public string? OutputJson { get; set; }
    public string? Error { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class MissionSuggestionPipelineMetadata
{
    public string PipelineStatus { get; set; } = "pending";
    public string ExecutionMode { get; set; } = "legacy";
    public string? FinalResultSource { get; set; }
    public bool UsedLegacyFallback { get; set; }
    public string? LegacyFallbackReason { get; set; }
    public Dictionary<string, MissionSuggestionStageSnapshot> Stages { get; set; } = [];
}

public class MissionSuggestionMetadata
{
    public string? OverallAssessment { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SpecialNotes { get; set; }
    public string? MixedRescueReliefWarning { get; set; }
    public bool NeedsManualReview { get; set; }
    public string? LowConfidenceWarning { get; set; }
    public bool NeedsAdditionalDepot { get; set; }
    public List<SupplyShortageDto>? SupplyShortages { get; set; }
    public List<SuggestedResourceDto>? SuggestedResources { get; set; }
    public string? SuggestedSeverityLevel { get; set; }
    public string? SuggestedMissionType { get; set; }
    public string? RawAiResponse { get; set; }
    public MissionSuggestionPipelineMetadata? Pipeline { get; set; }
}
