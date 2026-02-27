using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.LoginRescuer
{
    public record LoginRescuerCommand(
        string Email,
        string Password
    ) : IRequest<LoginRescuerResponse>;
}
