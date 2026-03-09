using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateRole;

public record UpdateRoleCommand(int RoleId, string Name) : IRequest<UpdateRoleResponse>;
