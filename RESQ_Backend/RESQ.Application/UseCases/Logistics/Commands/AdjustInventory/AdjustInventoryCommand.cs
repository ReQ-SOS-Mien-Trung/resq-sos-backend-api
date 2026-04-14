using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.AdjustInventory;

public record AdjustInventoryCommand(
    Guid UserId,
    int ItemModelId,
    int QuantityChange,
    string Reason,
    string? Note,
    DateTime? ExpiredDate, int? DepotId = null) : IRequest<AdjustInventoryResponse>;
