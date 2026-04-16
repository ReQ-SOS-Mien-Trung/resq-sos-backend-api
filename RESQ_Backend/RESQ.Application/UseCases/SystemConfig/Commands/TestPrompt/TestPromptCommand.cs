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
    string? Purpose,
    string? SystemPrompt,
    string? UserPromptTemplate,
    string? Version,
    bool? IsActive,
    int? AiConfigId
) : IRequest<TestPromptResponse>;
