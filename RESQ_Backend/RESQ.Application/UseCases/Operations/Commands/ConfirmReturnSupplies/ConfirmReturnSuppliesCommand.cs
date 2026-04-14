using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmReturnSupplies;

/// <summary>
/// Depot manager xác nh?n dã nh?n l?i v?t ph?m t? d?i c?u h?.
/// Chuy?n RETURN_SUPPLIES activity t? PendingConfirmation ? Succeed và restock kho.
/// </summary>
public record ConfirmReturnSuppliesCommand(
    int ActivityId,
    int MissionId,
    Guid ConfirmedBy,
    List<ActualReturnedConsumableItemDto> ConsumableItems,
    List<ActualReturnedReusableItemDto> ReusableItems,
    string? DiscrepancyNote
) : IRequest<ConfirmReturnSuppliesResponse>;
