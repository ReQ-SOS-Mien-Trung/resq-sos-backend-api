using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.CancelDepotClosureTransfer;

public class CancelDepotClosureTransferCommandHandler(
    IDepotClosureTransferRepository transferRepository,
    IDepotClosureRepository closureRepository,
    IDepotRepository depotRepository,
    IDepotInventoryRepository inventoryRepository,
    IOperationalHubService operationalHubService,
    IUnitOfWork unitOfWork,
    ILogger<CancelDepotClosureTransferCommandHandler> logger)
    : IRequestHandler<CancelDepotClosureTransferCommand, CancelDepotClosureTransferResponse>
{
    public async Task<CancelDepotClosureTransferResponse> Handle(
        CancelDepotClosureTransferCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "CancelDepotClosureTransfer | DepotId={DepotId} TransferId={TransferId} By={By}",
            request.DepotId,
            request.TransferId,
            request.CancelledBy);

        var transfer = await transferRepository.GetByIdAsync(request.TransferId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi chuyển kho #{request.TransferId}.");

        if (transfer.SourceDepotId != request.DepotId)
            throw new ConflictException("Bản ghi chuyển kho không thuộc kho nguồn này.");

        if (transfer.Status == "Received")
            throw new ConflictException("Không thể hủy vì quá trình chuyển hàng đã hoàn tất (Received).");

        if (transfer.Status == "Cancelled")
            throw new ConflictException("Bản ghi chuyển kho đã bị hủy trước đó.");

        transfer.Cancel(request.CancelledBy, request.Reason);

        var transferItems = await transferRepository.GetItemsByTransferIdAsync(transfer.Id, cancellationToken);
        var moveDtos = transferItems
            .Select(item => new DepotClosureTransferItemMoveDto
            {
                ItemModelId = item.ItemModelId,
                ItemType = item.ItemType,
                Quantity = item.Quantity
            })
            .ToList();

        var closure = await closureRepository.GetByIdAsync(transfer.ClosureId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi đóng kho #{transfer.ClosureId}.");

        var cancelledAt = DateTime.UtcNow;
        var requiresFurtherResolution = false;
        var remainingItemCount = 0;
        var closureAction = "TransferCancelled";

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await transferRepository.UpdateAsync(transfer, cancellationToken);

            if (moveDtos.Count > 0)
            {
                await inventoryRepository.ReleaseClosureReservationAsync(
                    transfer.SourceDepotId,
                    transfer.Id,
                    transfer.ClosureId,
                    request.CancelledBy,
                    moveDtos,
                    cancellationToken);
            }

            var hasOpenTransfers = await transferRepository.HasOpenTransfersAsync(closure.Id, cancellationToken);
            if (!hasOpenTransfers)
            {
                var remainingItems = await depotRepository.GetDetailedInventoryForClosureAsync(
                    transfer.SourceDepotId,
                    cancellationToken);

                remainingItemCount = remainingItems.Count;
                if (remainingItemCount > 0)
                {
                    closure.ReopenForResidualHandling();
                    requiresFurtherResolution = true;
                    closureAction = "ReopenedForResidualHandling";
                }
                else
                {
                    closure.Complete(cancelledAt);
                    closureAction = "ResolvedByTransfers";
                }
            }

            await closureRepository.UpdateAsync(closure, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "DepotClosureTransfer cancelled | TransferId={TransferId} DepotId={DepotId} ClosureStatus={ClosureStatus}",
            transfer.Id,
            request.DepotId,
            closure.Status);

        await operationalHubService.PushDepotClosureUpdateAsync(
            new DepotClosureRealtimeUpdate
            {
                SourceDepotId = request.DepotId,
                TargetDepotId = transfer.TargetDepotId,
                ClosureId = closure.Id,
                TransferId = transfer.Id,
                EntityType = "Transfer",
                Action = "Cancelled",
                Status = transfer.Status
            },
            cancellationToken);

        await operationalHubService.PushDepotClosureUpdateAsync(
            new DepotClosureRealtimeUpdate
            {
                SourceDepotId = request.DepotId,
                TargetDepotId = transfer.TargetDepotId,
                ClosureId = closure.Id,
                TransferId = transfer.Id,
                EntityType = "Closure",
                Action = closureAction,
                Status = closure.Status.ToString()
            },
            cancellationToken);

        return new CancelDepotClosureTransferResponse
        {
            TransferId = transfer.Id,
            DepotId = request.DepotId,
            TransferStatus = transfer.Status,
            ClosureId = closure.Id,
            ClosureStatus = closure.Status.ToString(),
            RequiresFurtherResolution = requiresFurtherResolution,
            RemainingItemCount = remainingItemCount,
            CancelledAt = cancelledAt,
            Message = requiresFurtherResolution
                ? "Đã hủy transfer. Không còn transfer mở nhưng kho nguồn vẫn còn hàng, admin cần chọn bước xử lý tiếp theo."
                : closure.CompletedAt.HasValue
                    ? "Đã hủy transfer. Phần hàng còn lại của closure đã được xử lý xong ở các transfer khác, kho nguồn chờ admin xác nhận đóng kho."
                    : "Đã hủy transfer. Các transfer khác của phiên đóng kho vẫn tiếp tục được xử lý."
        };
    }
}
