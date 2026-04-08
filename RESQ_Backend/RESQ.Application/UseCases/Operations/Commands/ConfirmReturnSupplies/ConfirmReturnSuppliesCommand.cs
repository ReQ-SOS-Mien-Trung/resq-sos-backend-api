using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmReturnSupplies;

/// <summary>
/// Depot manager xác nhận đã nhận lại vật tư từ đội cứu hộ.
/// Chuyển RETURN_SUPPLIES activity từ PendingConfirmation → Succeed và restock kho.
/// </summary>
public record ConfirmReturnSuppliesCommand(
    int ActivityId,
    int MissionId,
    Guid ConfirmedBy,
    List<ActualReturnedConsumableItemDto> ConsumableItems,
    List<ActualReturnedReusableItemDto> ReusableItems,
    string? DiscrepancyNote
) : IRequest<ConfirmReturnSuppliesResponse>;
