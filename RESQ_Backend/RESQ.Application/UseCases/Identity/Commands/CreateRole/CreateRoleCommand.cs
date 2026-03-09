using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.CreateRole;

public record CreateRoleCommand(string Name) : IRequest<CreateRoleResponse>;
