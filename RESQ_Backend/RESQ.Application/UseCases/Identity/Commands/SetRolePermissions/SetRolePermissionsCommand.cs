using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.SetRolePermissions;

public record SetRolePermissionsCommand(int RoleId, List<int> PermissionIds) : IRequest<SetRolePermissionsResponse>;
