using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.ReceiveClosureTransfer;

/// <summary>
/// Manager kho đích xác nhận nhận hàng.
/// Sau đó hệ thống bulk-transfer inventory và đánh dấu phiên xử lý hàng tồn đã xong,
/// nhưng vẫn chờ admin gọi POST /logistics/depot/{id}/close để đóng kho thật sự.
/// </summary>
public class ReceiveClosureTransferCommandHandler(
    IDepotClosureTransferRepository transferRepository,
    IDepotClosureRepository closureRepository,
    IDepotRepository depotRepository,
    IDepotInventoryRepository inventoryRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork,
    ILogger<ReceiveClosureTransferCommandHandler> logger)
    : IRequestHandler<ReceiveClosureTransferCommand, ReceiveClosureTransferResponse>
{
    public async Task<ReceiveClosureTransferResponse> Handle(
        ReceiveClosureTransferCommand request,
        CancellationToken cancellationToken)
    {
        var transfer = await transferRepository.GetByIdAsync(request.TransferId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi chuyển kho #{request.TransferId}.");

        var managerDepotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw ExceptionCodes.WithCode(
                new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động."),
                LogisticsErrorCodes.DepotManagerNotAssigned);

        if (managerDepotId != transfer.TargetDepotId)
            throw new ForbiddenException("Bạn không phải là manager của kho đích trong quá trình nhận hàng này.");

        var closure = await closureRepository.GetByIdAsync(transfer.ClosureId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi đóng kho #{transfer.ClosureId}.");

        var sourceDepot = await depotRepository.GetByIdAsync(transfer.SourceDepotId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy kho nguồn #{transfer.SourceDepotId}.");

        var transferItems = await transferRepository.GetItemsByTransferIdAsync(transfer.Id, cancellationToken);
        if (transferItems.Count == 0)
            throw new ConflictException("Transfer không có vật phẩm được cấu hình để nhận hàng.");

        transfer.MarkReceived(request.UserId, request.Note);
        var completedAt = DateTime.UtcNow;

        await inventoryRepository.TransferClosureItemsAsync(
            sourceDepotId: transfer.SourceDepotId,
            targetDepotId: transfer.TargetDepotId,
            closureId: transfer.ClosureId,
            transferId: transfer.Id,
            performedBy: request.UserId,
            items: transferItems.Select(x => new DepotClosureTransferItemMoveDto
            {
                ItemModelId = x.ItemModelId,
                ItemType = x.ItemType,
                Quantity = x.Quantity
            }).ToList(),
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "TransferClosureItems completed | ClosureId={ClosureId} TransferId={TransferId}",
            transfer.ClosureId, transfer.Id);

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await transferRepository.UpdateAsync(transfer, cancellationToken);

            var hasOpenTransfers = await transferRepository.HasOpenTransfersAsync(closure.Id, cancellationToken);
            if (!hasOpenTransfers)
            {
                closure.Complete(completedAt);
            }

            await closureRepository.UpdateAsync(closure, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "Depot closure transfer received | DepotId={DepotId} ClosureId={ClosureId} TransferId={TransferId}",
            transfer.SourceDepotId, closure.Id, transfer.Id);

        try
        {
            await firebaseService.SendNotificationToUserAsync(
                closure.InitiatedBy,
                closure.CompletedAt.HasValue ? "Xử lý hàng tồn đã hoàn tất" : "Đã hoàn tất một đợt chuyển kho",
                closure.CompletedAt.HasValue
                    ? $"Toàn bộ hàng tồn của kho '{sourceDepot.Name}' đã được chuyển xong theo kế hoạch. Kho vẫn ở trạng thái Unavailable và chờ admin xác nhận đóng kho."
                    : $"Transfer #{transfer.Id} từ kho '{sourceDepot.Name}' đã được nhận thành công. Vẫn còn các transfer khác chờ hoàn tất.",
                "depot_closure_completed",
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify admin | ClosureId={ClosureId}", closure.Id);
        }

        return new ReceiveClosureTransferResponse
        {
            TransferId = transfer.Id,
            ClosureId = closure.Id,
            TransferStatus = transfer.Status,
            ConsumableUnitsMoved = transfer.SnapshotConsumableUnits,
            ReusableItemsMoved = transfer.SnapshotReusableUnits,
            CompletedAt = completedAt,
            Message = closure.CompletedAt.HasValue
                ? "Đã xác nhận nhận hàng. Toàn bộ kế hoạch phân bổ hàng tồn đã hoàn tất, kho nguồn vẫn giữ trạng thái Unavailable và chờ admin xác nhận đóng kho."
                : "Đã xác nhận nhận hàng cho transfer này. Các transfer còn lại của phiên đóng kho vẫn tiếp tục được xử lý."
        };
    }
}

