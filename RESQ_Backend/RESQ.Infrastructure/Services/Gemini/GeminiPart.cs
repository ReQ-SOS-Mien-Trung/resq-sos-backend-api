using System.Text.Json.Serialization;

namespace RESQ.Infrastructure.Services.Gemini;

internal class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
