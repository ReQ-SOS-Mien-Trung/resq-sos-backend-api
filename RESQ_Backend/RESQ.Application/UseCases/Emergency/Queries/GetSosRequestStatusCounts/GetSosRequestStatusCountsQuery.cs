using MediatR;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequestStatusCounts;

public record GetSosRequestStatusCountsQuery(
    DateTime? From,
    DateTime? To
) : IRequest<GetSosRequestStatusCountsResponse>;
