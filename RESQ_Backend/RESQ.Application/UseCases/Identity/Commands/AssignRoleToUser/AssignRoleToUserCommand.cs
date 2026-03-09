using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.AssignRoleToUser;

public record AssignRoleToUserCommand(Guid UserId, int RoleId) : IRequest<AssignRoleToUserResponse>;
