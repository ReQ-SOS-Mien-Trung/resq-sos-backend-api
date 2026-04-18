using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.CancelDepotClosureTransfer;

/// <summary>
/// Admin hủy phiên chuyển kho đang chờ xử lý.
/// Kho nguồn vẫn giữ trạng thái Unavailable - admin tự chuyển lại nếu cần.
/// </summary>
public record CancelDepotClosureTransferCommand(
    int DepotId,
    int TransferId,
    Guid CancelledBy,
    string? Reason
) : IRequest<CancelDepotClosureTransferResponse>;
