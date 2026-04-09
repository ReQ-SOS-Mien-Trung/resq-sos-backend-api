using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetClosureTransfer;

/// <summary>
/// Lấy thông tin chi tiết bản ghi chuyển hàng khi đóng kho.
/// <para>
/// - Admin: truyền DepotId (sourceDepotId) từ route.
/// - Manager kho nguồn / kho đích: truyền RequestingUserId, hệ thống tự xác định depotId từ token.
/// </para>
/// </summary>
public record GetClosureTransferQuery(
    int DepotId,
    int TransferId,
    Guid? RequestingUserId = null) : IRequest<ClosureTransferResponse>;
