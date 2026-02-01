using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.Login
{
    public record LoginCommand  (
    string? Username,
    string? Phone,
    string Password
    ) : IRequest<LoginResonse>;
}
