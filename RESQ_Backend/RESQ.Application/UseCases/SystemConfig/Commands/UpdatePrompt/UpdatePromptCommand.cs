using MediatR;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;

public record UpdatePromptCommand(
    int Id,
    string? Name,
    PromptType? PromptType,
    AiProvider? Provider,
    string? Purpose,
    string? SystemPrompt,
    string? UserPromptTemplate,
    string? Model,
    double? Temperature,
    int? MaxTokens,
    string? Version,
    string? ApiUrl,
    string? ApiKey,
    bool? IsActive
) : IRequest;
