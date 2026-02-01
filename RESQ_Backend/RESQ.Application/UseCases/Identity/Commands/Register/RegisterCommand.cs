using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.Register
{
    public record RegisterCommand(
        string Phone,
        string Password
    ) : IRequest<RegisterResponse>;
}
