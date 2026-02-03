using MediatR;

namespace RESQ.Application.UseCases.SosRequests.Queries.GetAllSosRequests;

public record GetAllSosRequestsQuery() : IRequest<GetAllSosRequestsResponse>;