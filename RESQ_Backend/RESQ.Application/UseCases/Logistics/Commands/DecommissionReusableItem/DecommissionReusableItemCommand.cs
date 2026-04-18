using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.DecommissionReusableItem;

public record DecommissionReusableItemCommand(
    Guid UserId,
    int ReusableItemId,
    string? Note,
    int? DepotId = null) : IRequest<DecommissionReusableItemResponse>;
