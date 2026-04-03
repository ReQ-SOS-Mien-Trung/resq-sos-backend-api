using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetClosureTransfer;

/// <summary>Lấy thông tin chi tiết bản ghi chuyển hàng khi đóng kho.</summary>
public record GetClosureTransferQuery(
    int DepotId,
    int ClosureId,
    int TransferId) : IRequest<ClosureTransferResponse>;
