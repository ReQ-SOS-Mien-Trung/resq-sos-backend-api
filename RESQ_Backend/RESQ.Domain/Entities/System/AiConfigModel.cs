using RESQ.Domain.Enum.System;

namespace RESQ.Domain.Entities.System;

public class AiConfigModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AiProvider Provider { get; set; } = AiProvider.Gemini;
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 2048;
    public string ApiUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? Version { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static AiConfigModel Create(
        string name,
        AiProvider provider,
        string model,
        double temperature,
        int maxTokens,
        string apiUrl,
        string? apiKey,
        string version = "1.0")
    {
        return new AiConfigModel
        {
            Name = name,
            Provider = provider,
            Model = model,
            Temperature = temperature,
            MaxTokens = maxTokens,
            ApiUrl = apiUrl,
            ApiKey = apiKey,
            Version = version,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string? name = null,
        AiProvider? provider = null,
        string? model = null,
        double? temperature = null,
        int? maxTokens = null,
        string? apiUrl = null,
        string? apiKey = null,
        string? version = null,
        bool? isActive = null)
    {
        if (name != null) Name = name;
        if (provider.HasValue) Provider = provider.Value;
        if (model != null) Model = model;
        if (temperature.HasValue) Temperature = temperature.Value;
        if (maxTokens.HasValue) MaxTokens = maxTokens.Value;
        if (apiUrl != null) ApiUrl = apiUrl;
        if (apiKey != null) ApiKey = apiKey;
        if (version != null) Version = version;
        if (isActive.HasValue) IsActive = isActive.Value;
        UpdatedAt = DateTime.UtcNow;
    }
}
