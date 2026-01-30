using MediatR;

namespace RESQ.Application.UseCases.Users.Commands.Logout
{
    public record LogoutCommand(Guid UserId) : IRequest<LogoutResponse>;
}
