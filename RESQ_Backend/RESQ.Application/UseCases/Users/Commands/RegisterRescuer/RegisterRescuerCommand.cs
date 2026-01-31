using MediatR;

namespace RESQ.Application.UseCases.Users.Commands.RegisterRescuer
{
    public record RegisterRescuerCommand(
        string Email,
        string Password,
        string? FullName
    ) : IRequest<RegisterRescuerResponse>;
}
