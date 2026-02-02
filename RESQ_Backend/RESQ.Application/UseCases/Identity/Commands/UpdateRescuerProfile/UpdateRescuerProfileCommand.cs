using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile
{
    public record UpdateRescuerProfileCommand(
        Guid UserId,
        string FirstName,
        string? LastName,
        string Phone,
        string Address,
        string? Ward,
        string City,
        double? Latitude,
        double? Longitude
    ) : IRequest<UpdateRescuerProfileResponse>;
}
