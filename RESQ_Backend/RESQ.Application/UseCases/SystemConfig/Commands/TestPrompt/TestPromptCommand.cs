using MediatR;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;

public enum TestPromptDraftMode
{
    ExistingPromptDraft = 1,
    NewPromptDraft = 2
}

public record TestPromptCommand(
    int? Id,
    TestPromptDraftMode Mode,
    int ClusterId,
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
) : IRequest<TestPromptResponse>;
