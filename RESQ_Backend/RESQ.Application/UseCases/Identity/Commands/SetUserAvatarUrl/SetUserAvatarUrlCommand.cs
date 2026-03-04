using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.SetUserAvatarUrl
{
    public record SetUserAvatarUrlCommand(
        Guid UserId,
        string AvatarUrl
    ) : IRequest<SetUserAvatarUrlResponse>;
}
