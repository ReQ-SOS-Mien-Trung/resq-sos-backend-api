using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.DeletePermission;

public record DeletePermissionCommand(int PermissionId) : IRequest<Unit>;
