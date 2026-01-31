using MediatR;

namespace RESQ.Application.UseCases.Users.Commands.RegisterRescuer
{
    public record RegisterRescuerCommand(
        string Username,
        string Password
    ) : IRequest<RegisterRescuerResponse>;
}
