using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetPromptVersions;

public class GetPromptVersionsResponse
{
    public int SourcePromptId { get; set; }
    public PromptType PromptType { get; set; }
    public List<PromptVersionSummaryDto> Items { get; set; } = [];
}

public class PromptVersionSummaryDto
{
    public int Id { get; set; }
    public string Status { get; set; } = "Archived";
    public string Name { get; set; } = string.Empty;
    public PromptType PromptType { get; set; }
    public string? Version { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
