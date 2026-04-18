using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.UpdatePermission;

public record UpdatePermissionCommand(int PermissionId, string Code, string? Name, string? Description) : IRequest<UpdatePermissionResponse>;
