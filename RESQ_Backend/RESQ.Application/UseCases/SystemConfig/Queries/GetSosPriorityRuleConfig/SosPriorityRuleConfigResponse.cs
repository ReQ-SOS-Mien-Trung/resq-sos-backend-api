using System.Text.Json.Serialization;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;

public class SosPriorityRuleConfigResponse : SosPriorityRuleConfigDocument
{
    public int Id { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Draft";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("created_by")]
    public Guid? CreatedBy { get; set; }

    [JsonPropertyName("activated_at")]
    public DateTime? ActivatedAt { get; set; }

    [JsonPropertyName("activated_by")]
    public Guid? ActivatedBy { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public class SosPriorityRuleConfigVersionSummaryResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("config_version")]
    public string ConfigVersion { get; set; } = string.Empty;

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("created_by")]
    public Guid? CreatedBy { get; set; }

    [JsonPropertyName("activated_at")]
    public DateTime? ActivatedAt { get; set; }

    [JsonPropertyName("activated_by")]
    public Guid? ActivatedBy { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public class SosPriorityRuleConfigValidationResponse
{
    [JsonPropertyName("is_valid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = [];

    [JsonPropertyName("preview")]
    public SosPriorityRuleConfigPreviewResponse? Preview { get; set; }
}

public class SosPriorityRuleConfigPreviewResponse
{
    [JsonPropertyName("sos_request_id")]
    public int SosRequestId { get; set; }

    [JsonPropertyName("config_version")]
    public string ConfigVersion { get; set; } = string.Empty;

    [JsonPropertyName("priority_score")]
    public double PriorityScore { get; set; }

    [JsonPropertyName("priority_level")]
    public string PriorityLevel { get; set; } = string.Empty;

    [JsonPropertyName("breakdown")]
    public SosPriorityEvaluationDetails? Breakdown { get; set; }
}
