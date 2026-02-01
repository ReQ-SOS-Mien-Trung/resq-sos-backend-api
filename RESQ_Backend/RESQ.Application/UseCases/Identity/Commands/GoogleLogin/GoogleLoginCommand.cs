using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.GoogleLogin
{
    public record GoogleLoginCommand(
        string IdToken
    ) : IRequest<GoogleLoginResponse>;
}
