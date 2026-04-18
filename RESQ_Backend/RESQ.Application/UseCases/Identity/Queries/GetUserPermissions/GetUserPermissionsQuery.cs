using MediatR;

namespace RESQ.Application.UseCases.Identity.Queries.GetUserPermissions;

public record GetUserPermissionsQuery(Guid UserId) : IRequest<GetUserPermissionsResponse>;
