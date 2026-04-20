using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.MarkReusableItemAvailable;

public record MarkReusableItemAvailableCommand(
    Guid UserId,
    int DepotId,
    int ReusableItemId,
    string? Note) : IRequest<MarkReusableItemAvailableResponse>;
