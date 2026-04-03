using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.ReceiveClosureTransfer;

/// <summary>
/// Quản lý kho đích xác nhận nhận hàng — chuyển transfer từ Shipping → Completed.
/// Kích hoạt BulkTransfer và hoàn tất đóng kho.
/// </summary>
public record ReceiveClosureTransferCommand(
    int DepotId,
    int ClosureId,
    int TransferId,
    Guid UserId,
    string? Note) : IRequest<ReceiveClosureTransferResponse>;
