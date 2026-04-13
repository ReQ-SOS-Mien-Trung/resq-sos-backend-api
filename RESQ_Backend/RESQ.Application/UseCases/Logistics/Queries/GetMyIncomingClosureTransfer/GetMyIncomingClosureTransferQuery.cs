using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyIncomingClosureTransfer;

/// <summary>
/// Manager kho đích gọi để tự khám phá phiên nhận hàng từ kho nguồn đang đóng cửa.
/// Hệ thống tự xác định depot của manager từ token - không cần truyền bất kỳ ID nào.
/// Trả về transfer đang active (chưa Received / Cancelled) kèm đủ sourceDepotId, closureId,
/// transferId để manager gọi các endpoint tiếp theo (prepare / ship / receive).
/// </summary>
public record GetMyIncomingClosureTransferQuery(Guid UserId)
    : IRequest<MyIncomingClosureTransferResponse?>;
