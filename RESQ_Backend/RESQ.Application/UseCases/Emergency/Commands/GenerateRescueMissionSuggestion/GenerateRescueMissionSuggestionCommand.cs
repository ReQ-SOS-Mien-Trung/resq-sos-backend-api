using MediatR;

namespace RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;

public record GenerateRescueMissionSuggestionCommand(
    List<int> SosRequestIds,
    Guid RequestedByUserId
) : IRequest<GenerateRescueMissionSuggestionResponse>;
