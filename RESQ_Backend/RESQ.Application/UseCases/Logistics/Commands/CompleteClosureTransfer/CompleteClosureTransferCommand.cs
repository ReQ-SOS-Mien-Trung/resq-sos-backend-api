using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.CompleteClosureTransfer;

/// <summary>
/// Quản lý kho nguồn xác nhận đã xuất toàn bộ hàng - chuyển transfer từ Shipping → Completed.
/// </summary>
/// <param name="DepotId">Kho nguồn (route param)</param>
/// <param name="TransferId">Bản ghi transfer</param>
/// <param name="UserId">Người thực hiện (manager của kho nguồn)</param>
/// <param name="Note">Ghi chú (tuỳ chọn)</param>
public record CompleteClosureTransferCommand(
    int TransferId,
    Guid UserId,
    string? Note) : IRequest<CompleteClosureTransferResponse>;
