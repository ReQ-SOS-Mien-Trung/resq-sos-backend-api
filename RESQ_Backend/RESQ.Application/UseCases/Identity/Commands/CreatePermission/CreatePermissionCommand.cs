using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.CreatePermission;

public record CreatePermissionCommand(string Code, string? Name, string? Description) : IRequest<CreatePermissionResponse>;
