namespace RESQ.Domain.Entities.System;

public class PromptModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public string? SystemPrompt { get; set; }
    public string? UserPromptTemplate { get; set; }
    public string? Model { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public string? Version { get; set; }
    public DateTime? CreatedAt { get; set; }

    public PromptModel() { }

    public static PromptModel Create(
        string name,
        string purpose,
        string systemPrompt,
        string userPromptTemplate,
        string model = "gemini-2.5-flash",
        double temperature = 0.3,
        int maxTokens = 2048,
        string version = "1.0")
    {
        return new PromptModel
        {
            Name = name,
            Purpose = purpose,
            SystemPrompt = systemPrompt,
            UserPromptTemplate = userPromptTemplate,
            Model = model,
            Temperature = temperature,
            MaxTokens = maxTokens,
            Version = version,
            CreatedAt = DateTime.UtcNow
        };
    }
}
