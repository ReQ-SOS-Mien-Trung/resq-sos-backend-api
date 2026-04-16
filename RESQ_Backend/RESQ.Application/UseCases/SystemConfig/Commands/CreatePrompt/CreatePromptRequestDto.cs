using System.Text.Json.Serialization;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;

public class CreatePromptRequestDto
{
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

    /// <summary>Nếu true (mặc định), prompt này sẽ được kích hoạt và các prompt cùng loại sẽ bị tắt.</summary>
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;
}
