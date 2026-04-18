using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.SetUserPermissions;

public record SetUserPermissionsCommand(
    Guid TargetUserId,
    Guid AdminId,
    List<int> PermissionIds
) : IRequest<SetUserPermissionsResponse>;
