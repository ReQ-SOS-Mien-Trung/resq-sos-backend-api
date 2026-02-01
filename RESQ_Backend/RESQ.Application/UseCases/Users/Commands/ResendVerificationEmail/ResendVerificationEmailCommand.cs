using MediatR;

namespace RESQ.Application.UseCases.Users.Commands.ResendVerificationEmail
{
    public record ResendVerificationEmailCommand(string Email) : IRequest<ResendVerificationEmailResponse>;
}
