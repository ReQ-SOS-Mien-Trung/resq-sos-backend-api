using MediatR;
using RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile
{
    public record UpdateRescuerProfileCommand(
        Guid UserId,
        string FirstName,
        string? LastName,
        string Phone,
        string Address,
        string? Ward,
        string? District,
        string Province,
        double? Latitude,
        double? Longitude,
        List<DocumentDto>? Documents
    ) : IRequest<UpdateRescuerProfileResponse>;
}
