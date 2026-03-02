using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.ForgotPassword
{
    public record ForgotPasswordCommand(string Email) : IRequest<ForgotPasswordResponse>;
}
