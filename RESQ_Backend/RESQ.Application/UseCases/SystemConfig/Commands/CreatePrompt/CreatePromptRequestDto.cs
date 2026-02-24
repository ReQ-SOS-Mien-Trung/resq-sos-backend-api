using System.Text.Json.Serialization;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;

public class CreatePromptRequestDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

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
}
