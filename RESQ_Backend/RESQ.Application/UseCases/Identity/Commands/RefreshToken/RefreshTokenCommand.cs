using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.RefreshToken
{
    public record RefreshTokenCommand(
        string AccessToken,
        string RefreshToken
    ) : IRequest<RefreshTokenResponse>;
}
