using MediatR;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;

public record UpdatePromptCommand(
    int Id,
    string? Name,
    PromptType? PromptType,
    string? Purpose,
    string? SystemPrompt,
    string? UserPromptTemplate,
    string? Version,
    bool? IsActive
) : IRequest;
