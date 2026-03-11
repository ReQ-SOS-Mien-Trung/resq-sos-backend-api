using MediatR;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Emergency.Queries.StreamRescueMissionSuggestion;

public record StreamRescueMissionSuggestionQuery(int ClusterId) : IStreamRequest<SseMissionEvent>;
