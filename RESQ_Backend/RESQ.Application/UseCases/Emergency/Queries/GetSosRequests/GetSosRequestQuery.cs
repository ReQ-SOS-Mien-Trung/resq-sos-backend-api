using MediatR;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequests;

public record GetSosRequestQuery(int Id, Guid RequestingUserId, bool HasPrivilegedAccess) : IRequest<GetSosRequestResponse>;