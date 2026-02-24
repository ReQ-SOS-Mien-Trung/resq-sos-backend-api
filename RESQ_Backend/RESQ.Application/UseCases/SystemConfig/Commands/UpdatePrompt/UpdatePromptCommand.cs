using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;

public record UpdatePromptCommand(
    int Id,
    string? Name,
    string? Purpose,
    string? SystemPrompt,
    string? UserPromptTemplate,
    string? Model,
    double? Temperature,
    int? MaxTokens,
    string? Version,
    string? ApiUrl,
    bool? IsActive
) : IRequest;
