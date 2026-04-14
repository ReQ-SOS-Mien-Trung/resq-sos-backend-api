using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetClosureTransfer;

public class GetClosureTransferQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotClosureTransferRepository transferRepository,
    IDepotInventoryRepository inventoryRepository)
    : IRequestHandler<GetClosureTransferQuery, ClosureTransferResponse>
{
    public async Task<ClosureTransferResponse> Handle(
        GetClosureTransferQuery request,
        CancellationToken cancellationToken)
    {
        var transfer = await transferRepository.GetByIdAsync(request.TransferId, cancellationToken)
            ?? throw new NotFoundException($"Kh�ng t�m th?y b?n ghi chuy?n kho #{request.TransferId}.");
        var transferItems = await transferRepository.GetItemsByTransferIdAsync(transfer.Id, cancellationToken);

        if (request.RequestingUserId.HasValue)
        {
            var managerDepotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(
                request.RequestingUserId.Value, cancellationToken);

            if (!managerDepotId.HasValue)
            {
                throw ExceptionCodes.WithCode(
                    new ForbiddenException("T�i kho?n qu?n l� kho chua du?c g�n kho ph? tr�ch."),
                    LogisticsErrorCodes.DepotManagerNotAssigned);
            }

            if (managerDepotId != transfer.SourceDepotId && managerDepotId != transfer.TargetDepotId)
            {
                throw new ForbiddenException("B?n kh�ng ph?i l� manager c?a kho ngu?n ho?c kho d�ch trong b?n ghi chuy?n h�ng n�y.");
            }
        }
        else if (transfer.SourceDepotId != request.DepotId)
        {
            throw new ConflictException("B?n ghi chuy?n kho kh�ng kh?p v?i th�ng tin du?c cung c?p.");
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
            CancellationReason = transfer.CancellationReason,
            Items = transferItems.Select(item => new ClosureTransferItemResponse
            {
                ItemModelId = item.ItemModelId,
                ItemName = item.ItemName,
                ItemType = item.ItemType,
                Unit = item.Unit,
                Quantity = item.Quantity
            }).ToList()
        };
    }
}

