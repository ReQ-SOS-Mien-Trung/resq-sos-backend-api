using MediatR;

namespace RESQ.Application.UseCases.Users.Commands.GoogleLogin
{
    public record GoogleLoginCommand(
        string IdToken
    ) : IRequest<GoogleLoginResponse>;
}
