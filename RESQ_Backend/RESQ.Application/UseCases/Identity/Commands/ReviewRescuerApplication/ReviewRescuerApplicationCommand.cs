using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.ReviewRescuerApplication
{
    public record ReviewRescuerApplicationCommand(
        int ApplicationId,
        Guid ReviewedBy,
        bool IsApproved,
        string? AdminNote
    ) : IRequest<ReviewRescuerApplicationResponse>;
}
