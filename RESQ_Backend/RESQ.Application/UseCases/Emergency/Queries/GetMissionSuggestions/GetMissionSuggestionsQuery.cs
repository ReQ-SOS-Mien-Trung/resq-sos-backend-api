using MediatR;

namespace RESQ.Application.UseCases.Emergency.Queries.GetMissionSuggestions;

public record GetMissionSuggestionsQuery(int ClusterId) : IRequest<GetMissionSuggestionsResponse>;
