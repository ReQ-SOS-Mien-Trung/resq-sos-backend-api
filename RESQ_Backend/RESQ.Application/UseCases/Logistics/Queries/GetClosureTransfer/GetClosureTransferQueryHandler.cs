using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetClosureTransfer;

public class GetClosureTransferQueryHandler(
    IDepotClosureTransferRepository transferRepository,
    IDepotInventoryRepository inventoryRepository)
    : IRequestHandler<GetClosureTransferQuery, ClosureTransferResponse>
{
    public async Task<ClosureTransferResponse> Handle(
        GetClosureTransferQuery request,
        CancellationToken cancellationToken)
    {
        var transfer = await transferRepository.GetByIdAsync(request.TransferId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi chuyển kho #{request.TransferId}.");

        // Nếu có userId (manager gọi) → tự xác định depot từ token, cho phép cả kho nguồn lẫn kho đích
        if (request.RequestingUserId.HasValue)
        {
            var managerDepotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(
                request.RequestingUserId.Value, cancellationToken);

            if (managerDepotId.HasValue)
            {
                if (managerDepotId != transfer.SourceDepotId && managerDepotId != transfer.TargetDepotId)
                    throw new ForbiddenException("Bạn không phải manager của kho nguồn hoặc kho đích trong bản ghi chuyển hàng này.");
            }
            else
            {
                // Không phải manager (admin) → kiểm tra theo DepotId truyền vào
                if (transfer.SourceDepotId != request.DepotId)
                    throw new ConflictException("Bản ghi chuyển kho không khớp với thông tin được cung cấp.");
            }
        }
        else
        {
            // Không có userId → kiểm tra theo DepotId (backward compatible)
            if (transfer.SourceDepotId != request.DepotId)
                throw new ConflictException("Bản ghi chuyển kho không khớp với thông tin được cung cấp.");
        }

        return new ClosureTransferResponse
        {
            Id = transfer.Id,
            ClosureId = transfer.ClosureId,
            SourceDepotId = transfer.SourceDepotId,
            TargetDepotId = transfer.TargetDepotId,
            Status = transfer.Status,
            CreatedAt = transfer.CreatedAt,
            SnapshotConsumableUnits = transfer.SnapshotConsumableUnits,
            SnapshotReusableUnits = transfer.SnapshotReusableUnits,
            ShippedAt = transfer.ShippedAt,
            ShippedBy = transfer.ShippedBy,
            ShipNote = transfer.ShipNote,
            ReceivedAt = transfer.ReceivedAt,
            ReceivedBy = transfer.ReceivedBy,
            ReceiveNote = transfer.ReceiveNote,
            CancelledAt = transfer.CancelledAt,
            CancelledBy = transfer.CancelledBy,
            CancellationReason = transfer.CancellationReason
        };
    }
}
