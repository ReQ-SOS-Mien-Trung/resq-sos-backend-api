using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication
{
    public record SubmitRescuerApplicationCommand(
        Guid UserId,
        string RescuerType,
        string FullName,
        string? Phone,
        string? Address,
        string? Ward,
        string? City,
        string? Note,
        List<DocumentDto>? Documents
    ) : IRequest<SubmitRescuerApplicationResponse>;
}
