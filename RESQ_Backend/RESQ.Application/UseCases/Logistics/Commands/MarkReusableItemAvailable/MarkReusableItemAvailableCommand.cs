using MediatR;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.MarkReusableItemAvailable;

public record MarkReusableItemAvailableCommand(
    Guid UserId,
    int DepotId,
    int ReusableItemId,
    ReusableItemCondition Condition,
    string? Note) : IRequest<MarkReusableItemAvailableResponse>;
