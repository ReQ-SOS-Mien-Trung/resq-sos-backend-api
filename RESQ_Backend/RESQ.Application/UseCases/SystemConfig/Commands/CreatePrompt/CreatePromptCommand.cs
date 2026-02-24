using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;

public record CreatePromptCommand(
    string Name,
    string Purpose,
    string SystemPrompt,
    string UserPromptTemplate,
    string Model,
    double Temperature,
    int MaxTokens,
    string Version,
    string? ApiUrl
) : IRequest<CreatePromptResponse>;
