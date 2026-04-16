using System.Text.Json.Serialization;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;

public class UpdatePromptRequestDto
{
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
}
