using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.PromptVersioning;

public class PromptVersionActionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PromptType PromptType { get; set; }
    public string? Version { get; set; }
    public string Status { get; set; } = "Archived";
    public string Message { get; set; } = string.Empty;
}
