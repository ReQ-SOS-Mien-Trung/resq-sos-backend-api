using MediatR;

namespace RESQ.Application.UseCases.Users.Commands.VerifyEmail
{
    public record VerifyEmailCommand(string Token) : IRequest<VerifyEmailResponse>;
}
