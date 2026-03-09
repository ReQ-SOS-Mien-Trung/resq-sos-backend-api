using RESQ.Domain.Enum.System;

namespace RESQ.Domain.Entities.System;

public class PromptModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PromptType PromptType { get; set; }
    public string? Purpose { get; set; }
    public string? SystemPrompt { get; set; }
    public string? UserPromptTemplate { get; set; }
    public string? Model { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public string? Version { get; set; }
    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public PromptModel() { }

    public static PromptModel Create(
        string name,
        PromptType promptType,
        string purpose,
        string systemPrompt,
        string userPromptTemplate,
        string model = "gemini-2.5-flash",
        double temperature = 0.3,
        int maxTokens = 2048,
        string version = "1.0",
        string? apiUrl = null,
        string? apiKey = null)
    {
        return new PromptModel
        {
            Name = name,
            PromptType = promptType,
            Purpose = purpose,
            SystemPrompt = systemPrompt,
            UserPromptTemplate = userPromptTemplate,
            Model = model,
            Temperature = temperature,
            MaxTokens = maxTokens,
            Version = version,
            ApiUrl = apiUrl,
            ApiKey = apiKey,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string? name = null,
        PromptType? promptType = null,
        string? purpose = null,
        string? systemPrompt = null,
        string? userPromptTemplate = null,
        string? model = null,
        double? temperature = null,
        int? maxTokens = null,
        string? version = null,
        string? apiUrl = null,
        string? apiKey = null,
        bool? isActive = null)
    {
        if (name != null) Name = name;
        if (promptType.HasValue) PromptType = promptType.Value;
        if (purpose != null) Purpose = purpose;
        if (systemPrompt != null) SystemPrompt = systemPrompt;
        if (userPromptTemplate != null) UserPromptTemplate = userPromptTemplate;
        if (model != null) Model = model;
        if (temperature.HasValue) Temperature = temperature.Value;
        if (maxTokens.HasValue) MaxTokens = maxTokens.Value;
        if (version != null) Version = version;
        if (apiUrl != null) ApiUrl = apiUrl;
        if (apiKey != null) ApiKey = apiKey;
        if (isActive.HasValue) IsActive = isActive.Value;
        UpdatedAt = DateTime.UtcNow;
    }
}
