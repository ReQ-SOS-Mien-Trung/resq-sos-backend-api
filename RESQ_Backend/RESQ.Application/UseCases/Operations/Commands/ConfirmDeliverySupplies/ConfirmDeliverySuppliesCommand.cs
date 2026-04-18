using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;

/// <summary>
/// Team thành viên xác nhận đã giao vật phẩm, kèm số lượng thực tế từng mặt hàng.
/// Chuyển DELIVER_SUPPLIES activity từ OnGoing → Succeed và tự động tạo RETURN_SUPPLIES nếu có surplus.
/// </summary>
public record ConfirmDeliverySuppliesCommand(
    int ActivityId,
    int MissionId,
    Guid ConfirmedBy,
    List<ActualDeliveredItemDto> ActualDeliveredItems,
    string? DeliveryNote
) : IRequest<ConfirmDeliverySuppliesResponse>;
