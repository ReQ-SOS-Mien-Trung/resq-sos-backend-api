using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.Logout
{
    public record LogoutCommand(Guid UserId) : IRequest<LogoutResponse>;
}
