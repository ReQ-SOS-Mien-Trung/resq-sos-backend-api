using MediatR;

namespace RESQ.Application.UseCases.Emergency.Queries.GetMySosRequests;

public record GetMySosRequestsQuery(Guid UserId) : IRequest<GetMySosRequestsResponse>;