using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetClosureTransfer;

public class GetClosureTransferQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotRepository depotRepository,
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
        var transferItems = await transferRepository.GetItemsByTransferIdAsync(transfer.Id, cancellationToken);
        var routeDepotMatchesTransfer = request.DepotId == transfer.SourceDepotId || request.DepotId == transfer.TargetDepotId;

        if (request.ClosureId.HasValue && transfer.ClosureId != request.ClosureId.Value)
        {
            throw new NotFoundException("Không tìm thấy bản ghi chuyển kho thuộc phiên đóng kho được yêu cầu.");
        }

        if (request.RequestingUserId.HasValue)
        {
            var managerDepotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(
                request.RequestingUserId.Value, cancellationToken);

            if (managerDepotId.HasValue
                && managerDepotId != transfer.SourceDepotId
                && managerDepotId != transfer.TargetDepotId)
            {
                throw new ForbiddenException("Bạn không phải là manager của kho nguồn hoặc kho đích trong bản ghi chuyển hàng này.");
            }
            if (!managerDepotId.HasValue && !routeDepotMatchesTransfer)
            {
                throw ExceptionCodes.WithCode(
                    new ForbiddenException("Tài khoản không có quyền xem bản ghi chuyển hàng này."),
                    LogisticsErrorCodes.DepotManagerNotAssigned);
            }
        }
        else if (!routeDepotMatchesTransfer)
        {
            throw new ConflictException("Bản ghi chuyển kho không khớp với thông tin được cung cấp.");
        }

        if (!routeDepotMatchesTransfer)
        {
            throw new NotFoundException("Không tìm thấy bản ghi chuyển kho thuộc kho được yêu cầu.");
        }

        var sourceDepot = await depotRepository.GetByIdAsync(transfer.SourceDepotId, cancellationToken);
        var targetDepot = await depotRepository.GetByIdAsync(transfer.TargetDepotId, cancellationToken);

        return new ClosureTransferResponse
        {
            Id = transfer.Id,
            ClosureId = transfer.ClosureId,
            SourceDepotId = transfer.SourceDepotId,
            SourceDepotName = sourceDepot?.Name,
            TargetDepotId = transfer.TargetDepotId,
            TargetDepotName = targetDepot?.Name,
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

