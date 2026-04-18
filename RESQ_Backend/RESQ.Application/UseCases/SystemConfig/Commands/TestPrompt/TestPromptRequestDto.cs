using System.Text.Json.Serialization;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;

public class TestPromptRequestDto
{
    [JsonPropertyName("clusterId")]
    public int ClusterId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("prompt_type")]
    public PromptType? PromptType { get; set; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }

    [JsonPropertyName("system_prompt")]
    public string? SystemPrompt { get; set; }

    [JsonPropertyName("user_prompt_template")]
    public string? UserPromptTemplate { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }

    [JsonPropertyName("ai_config_id")]
    public int? AiConfigId { get; set; }
}

public class TestNewPromptRequestDto
{
    [JsonPropertyName("clusterId")]
    public int ClusterId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("prompt_type")]
    public PromptType PromptType { get; set; }

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = null!;

    [JsonPropertyName("system_prompt")]
    public string SystemPrompt { get; set; } = null!;

    [JsonPropertyName("user_prompt_template")]
    public string UserPromptTemplate { get; set; } = null!;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("ai_config_id")]
    public int? AiConfigId { get; set; }
}
