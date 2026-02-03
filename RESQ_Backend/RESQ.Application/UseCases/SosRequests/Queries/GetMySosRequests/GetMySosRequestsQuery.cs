using MediatR;

namespace RESQ.Application.UseCases.SosRequests.Queries.GetMySosRequests;

public record GetMySosRequestsQuery(Guid UserId) : IRequest<GetMySosRequestsResponse>;