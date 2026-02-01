using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.RegisterRescuer
{
    public record RegisterRescuerCommand(
        string Email,
        string Password
    ) : IRequest<RegisterRescuerResponse>;
}
