using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.UnbanUser;

public record UnbanUserCommand(Guid TargetUserId) : IRequest<UnbanUserResponse>;
