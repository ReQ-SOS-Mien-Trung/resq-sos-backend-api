using MediatR;

namespace RESQ.Application.UseCases.Users.Commands.Login
{
    public record LoginCommand  (
    string? Username,
    string? Phone,
    string Password
    ) : IRequest<LoginResonse>;
}
