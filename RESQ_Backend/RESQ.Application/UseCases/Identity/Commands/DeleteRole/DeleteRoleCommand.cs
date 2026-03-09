using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.DeleteRole;

public record DeleteRoleCommand(int RoleId) : IRequest<Unit>;
