using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.VerifyEmail
{
    public record VerifyEmailCommand(string Token) : IRequest<VerifyEmailResponse>;
}
