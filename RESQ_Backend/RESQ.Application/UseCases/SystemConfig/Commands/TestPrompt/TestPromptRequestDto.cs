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

    [JsonPropertyName("provider")]
    public AiProvider? Provider { get; set; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }

    [JsonPropertyName("system_prompt")]
    public string? SystemPrompt { get; set; }

    [JsonPropertyName("user_prompt_template")]
    public string? UserPromptTemplate { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("api_url")]
    public string? ApiUrl { get; set; }

    [JsonPropertyName("api_key")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }
}

public class TestNewPromptRequestDto
{
    [JsonPropertyName("clusterId")]
    public int ClusterId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("prompt_type")]
    public PromptType PromptType { get; set; }

    [JsonPropertyName("provider")]
    public AiProvider Provider { get; set; } = AiProvider.Gemini;

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = null!;

    [JsonPropertyName("system_prompt")]
    public string SystemPrompt { get; set; } = null!;

    [JsonPropertyName("user_prompt_template")]
    public string UserPromptTemplate { get; set; } = null!;

    [JsonPropertyName("model")]
    public string Model { get; set; } = "gemini-2.5-flash";

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.3;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 2048;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("api_url")]
    public string? ApiUrl { get; set; }

    [JsonPropertyName("api_key")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;
}
