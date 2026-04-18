using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication
{
    public record SubmitRescuerApplicationCommand(
        Guid UserId,
        string RescuerType,
        string FirstName,
        string LastName,
        string? Phone,
        string? Address,
        string? Ward,
        string? Province,
        double? Latitude,
        double? Longitude,
        string? Note
    ) : IRequest<SubmitRescuerApplicationResponse>;
}
