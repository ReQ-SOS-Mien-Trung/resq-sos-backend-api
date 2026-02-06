using MediatR;

namespace RESQ.Application.UseCases.Emergency.Queries.GetAllSosRequests;

public record GetAllSosRequestsQuery() : IRequest<GetAllSosRequestsResponse>;