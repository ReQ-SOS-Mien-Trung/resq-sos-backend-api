using MediatR;

namespace RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;

public record GenerateRescueMissionSuggestionCommand(
    int ClusterId,
    Guid RequestedByUserId
) : IRequest<GenerateRescueMissionSuggestionResponse>;
