using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;

/// <summary>
/// Team thành viên xác nh?n dã giao v?t ph?m, kèm s? lu?ng th?c t? t?ng m?t hàng.
/// Chuy?n DELIVER_SUPPLIES activity t? OnGoing ? Succeed và t? d?ng t?o RETURN_SUPPLIES n?u có surplus.
/// </summary>
public record ConfirmDeliverySuppliesCommand(
    int ActivityId,
    int MissionId,
    Guid ConfirmedBy,
    List<ActualDeliveredItemDto> ActualDeliveredItems,
    string? DeliveryNote
) : IRequest<ConfirmDeliverySuppliesResponse>;
