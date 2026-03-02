using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.ResetPassword
{
    public record ResetPasswordCommand(string Token, string NewPassword)
        : IRequest<ResetPasswordResponse>;
}
