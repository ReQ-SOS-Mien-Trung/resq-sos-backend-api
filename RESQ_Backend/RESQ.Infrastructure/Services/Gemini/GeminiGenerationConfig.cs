using System.Text.Json.Serialization;

namespace RESQ.Infrastructure.Services.Gemini;

internal class GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; set; }
}
