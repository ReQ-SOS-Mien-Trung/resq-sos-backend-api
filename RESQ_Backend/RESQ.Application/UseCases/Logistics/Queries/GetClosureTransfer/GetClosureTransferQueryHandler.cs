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
            ?? throw new NotFoundException($"Không tìm th?y b?n ghi chuy?n kho #{request.TransferId}.");

        if (request.RequestingUserId.HasValue)
        {
            var managerDepotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(
                request.RequestingUserId.Value, cancellationToken);

            if (!managerDepotId.HasValue)
            {
                throw ExceptionCodes.WithCode(
                    new ForbiddenException("TÃ i khoáº£n quáº£n lÃ½ kho chÆ°a Ä‘Æ°á»£c gÃ¡n kho phá»¥ trÃ¡ch."),
                    LogisticsErrorCodes.DepotManagerNotAssigned);
            }

            if (managerDepotId != transfer.SourceDepotId && managerDepotId != transfer.TargetDepotId)
            {
                throw new ForbiddenException("B?n không ph?i manager c?a kho ngu?n ho?c kho dích trong ban ghi chuyen hang nay.");
            }
        }
        else if (transfer.SourceDepotId != request.DepotId)
        {
            throw new ConflictException("B?n ghi chuy?n kho không kh?p v?i thông tin du?c cung c?p.");
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
