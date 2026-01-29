using MediatR;

namespace RESQ.Application.UseCases.Users.Commands.RefreshToken
{
    public record RefreshTokenCommand(
        string AccessToken,
        string RefreshToken
    ) : IRequest<RefreshTokenResponse>;
}
