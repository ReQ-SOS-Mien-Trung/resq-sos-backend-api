using MediatR;

namespace RESQ.Application.UseCases.Identity.Queries.GetRolePermissions;

public record GetRolePermissionsQuery(int RoleId) : IRequest<GetRolePermissionsResponse>;
