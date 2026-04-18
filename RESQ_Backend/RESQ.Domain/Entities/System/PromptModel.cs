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
    public string? Version { get; set; }
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
        string version = "1.0")
    {
        return new PromptModel
        {
            Name = name,
            PromptType = promptType,
            Purpose = purpose,
            SystemPrompt = systemPrompt,
            UserPromptTemplate = userPromptTemplate,
            Version = version,
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
        string? version = null,
        bool? isActive = null)
    {
        if (name != null) Name = name;
        if (promptType.HasValue) PromptType = promptType.Value;
        if (purpose != null) Purpose = purpose;
        if (systemPrompt != null) SystemPrompt = systemPrompt;
        if (userPromptTemplate != null) UserPromptTemplate = userPromptTemplate;
        if (version != null) Version = version;
        if (isActive.HasValue) IsActive = isActive.Value;
        UpdatedAt = DateTime.UtcNow;
    }
}
