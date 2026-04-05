using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.ShipClosureTransfer;

/// <summary>
/// Quản lý kho nguồn xác nhận xuất hàng — chuyển transfer từ AwaitingShipment → Shipping.
/// </summary>
/// <param name="DepotId">Kho nguồn (route param)</param>
/// <param name="ClosureId">Bản ghi đóng kho</param>
/// <param name="TransferId">Bản ghi transfer</param>
/// <param name="UserId">Người thực hiện (manager của kho nguồn)</param>
/// <param name="Note">Ghi chú xuất hàng (tuỳ chọn)</param>
public record ShipClosureTransferCommand(
    int DepotId,
    int ClosureId,
    int TransferId,
    Guid UserId,
    string? Note) : IRequest<ShipClosureTransferResponse>;
