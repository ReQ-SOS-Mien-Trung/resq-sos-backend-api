using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile
{
    public record UpdateRescuerProfileCommand(
        Guid UserId,
        string? FirstName,
        string? LastName,
        string? Address,
        string? Ward,
        string? Province,
        double? Latitude,
        double? Longitude,
        string? AvatarUrl
    ) : IRequest<UpdateRescuerProfileResponse>;
}
