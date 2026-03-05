using MediatR;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;

public record CreatePromptCommand(
    string Name,
    PromptType PromptType,
    string Purpose,
    string SystemPrompt,
    string UserPromptTemplate,
    string Model,
    double Temperature,
    int MaxTokens,
    string Version,
    string? ApiUrl,
    bool IsActive = true
) : IRequest<CreatePromptResponse>;
