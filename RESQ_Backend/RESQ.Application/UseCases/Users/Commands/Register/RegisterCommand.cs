using MediatR;

namespace RESQ.Application.UseCases.Users.Commands.Register
{
    public record RegisterCommand(
        string Phone,
        string Password
    ) : IRequest<RegisterResponse>;
}
