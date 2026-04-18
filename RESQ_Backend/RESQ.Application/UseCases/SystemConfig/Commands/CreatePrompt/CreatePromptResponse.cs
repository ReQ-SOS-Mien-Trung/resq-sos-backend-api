using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;

public class CreatePromptResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PromptType PromptType { get; set; }
    public string Message { get; set; } = string.Empty;
}
