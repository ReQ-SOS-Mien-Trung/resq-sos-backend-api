using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetPromptById;

public class GetPromptByIdResponse
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
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
