using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Common.Constants;
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
            ?? throw new NotFoundException($"Khong tim thay ban ghi chuyen kho #{request.TransferId}.");

        if (request.RequestingUserId.HasValue)
        {
            var managerDepotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(
                request.RequestingUserId.Value, cancellationToken);

            if (!managerDepotId.HasValue)
            {
                throw ExceptionCodes.WithCode(
                    new ForbiddenException("Tài khoản quản lý kho chưa được gán kho phụ trách."),
                    LogisticsErrorCodes.DepotManagerNotAssigned);
            }

            if (managerDepotId != transfer.SourceDepotId && managerDepotId != transfer.TargetDepotId)
            {
                throw new ForbiddenException("Ban khong phai manager cua kho nguon hoac kho dich trong ban ghi chuyen hang nay.");
            }
        }
        else if (transfer.SourceDepotId != request.DepotId)
        {
            throw new ConflictException("Ban ghi chuyen kho khong khop voi thong tin duoc cung cap.");
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
