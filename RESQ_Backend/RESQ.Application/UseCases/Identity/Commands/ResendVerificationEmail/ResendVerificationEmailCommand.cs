using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.ResendVerificationEmail
{
    public record ResendVerificationEmailCommand(string Email) : IRequest<ResendVerificationEmailResponse>;
}
