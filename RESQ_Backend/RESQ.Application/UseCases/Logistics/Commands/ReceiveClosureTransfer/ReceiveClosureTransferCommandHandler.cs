using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.ReceiveClosureTransfer;

public class ReceiveClosureTransferCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotClosureTransferRepository transferRepository,
    IDepotClosureRepository closureRepository,
    IDepotRepository depotRepository,
    IDepotInventoryRepository inventoryRepository,
    IUserRepository userRepository,
    IFirebaseService firebaseService,
    IOperationalHubService operationalHubService,
    IUnitOfWork unitOfWork,
    ILogger<ReceiveClosureTransferCommandHandler> logger)
    : IRequestHandler<ReceiveClosureTransferCommand, ReceiveClosureTransferResponse>
{
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<ReceiveClosureTransferResponse> Handle(
        ReceiveClosureTransferCommand request,
        CancellationToken cancellationToken)
    {
        var transfer = await transferRepository.GetByIdAsync(request.TransferId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi chuyển kho #{request.TransferId}.");

        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(
                request.UserId,
                request.DepotId,
                cancellationToken)
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

        var completedAt = DateTime.UtcNow;
        var requiresFurtherResolution = false;
        var remainingItemCount = 0;
        var closureAction = "TransferReceived";

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
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
                transfer.ClosureId,
                transfer.Id);

            transfer.MarkReceived(request.UserId, request.Note);
            await transferRepository.UpdateAsync(transfer, cancellationToken);

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
                    closure.Complete(completedAt);
                    closureAction = "ResolvedByTransfers";
                }
            }

            await closureRepository.UpdateAsync(closure, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "Depot closure transfer received | DepotId={DepotId} ClosureId={ClosureId} TransferId={TransferId}",
            transfer.SourceDepotId,
            closure.Id,
            transfer.Id);

        try
        {
            var recipientIds = new HashSet<Guid> { closure.InitiatedBy };

            foreach (var adminId in await userRepository.GetActiveAdminUserIdsAsync(cancellationToken))
            {
                recipientIds.Add(adminId);
            }

            var sourceManagerId = await inventoryRepository.GetActiveManagerUserIdByDepotIdAsync(
                transfer.SourceDepotId,
                cancellationToken);
            if (sourceManagerId.HasValue)
            {
                recipientIds.Add(sourceManagerId.Value);
            }

            var targetManagerId = await inventoryRepository.GetActiveManagerUserIdByDepotIdAsync(
                transfer.TargetDepotId,
                cancellationToken);
            if (targetManagerId.HasValue)
            {
                recipientIds.Add(targetManagerId.Value);
            }

            var title = requiresFurtherResolution
                ? "Đợt chuyển kho đã nhận xong, kho nguồn còn hàng cần xử lý tiếp"
                : closure.CompletedAt.HasValue
                    ? "Đợt chuyển kho đã nhận xong, closure chờ xác nhận đóng kho"
                    : "Đợt chuyển kho đã được kho đích xác nhận nhận hàng";

            var body = requiresFurtherResolution
                ? $"Transfer #{transfer.Id} từ kho '{sourceDepot.Name}' đã nhận xong, nhưng kho nguồn vẫn còn hàng. Admin cần chọn bước xử lý tiếp theo."
                : closure.CompletedAt.HasValue
                    ? $"Transfer #{transfer.Id} từ kho '{sourceDepot.Name}' đã nhận xong và toàn bộ phần hàng chuyển kho đã được xử lý. Kho nguồn đang chờ admin xác nhận đóng kho."
                    : $"Transfer #{transfer.Id} từ kho '{sourceDepot.Name}' đã được kho đích xác nhận nhận hàng thành công.";

            var notificationData = new Dictionary<string, string>
            {
                ["closureId"] = closure.Id.ToString(),
                ["transferId"] = transfer.Id.ToString(),
                ["sourceDepotId"] = transfer.SourceDepotId.ToString(),
                ["targetDepotId"] = transfer.TargetDepotId.ToString(),
                ["requiresFurtherResolution"] = requiresFurtherResolution.ToString().ToLowerInvariant(),
                ["remainingItemCount"] = remainingItemCount.ToString(),
                ["closureStatus"] = closure.Status.ToString(),
                ["transferStatus"] = transfer.Status
            };

            foreach (var recipientId in recipientIds)
            {
                await firebaseService.SendNotificationToUserAsync(
                    recipientId,
                    title,
                    body,
                    "depot_closure_transfer_received",
                    notificationData,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to notify closure transfer recipients | ClosureId={ClosureId} TransferId={TransferId}",
                closure.Id,
                transfer.Id);
        }

        await operationalHubService.PushDepotClosureUpdateAsync(
            new DepotClosureRealtimeUpdate
            {
                SourceDepotId = transfer.SourceDepotId,
                TargetDepotId = transfer.TargetDepotId,
                ClosureId = closure.Id,
                TransferId = transfer.Id,
                EntityType = "Transfer",
                Action = "Received",
                Status = transfer.Status
            },
            cancellationToken);

        await operationalHubService.PushDepotClosureUpdateAsync(
            new DepotClosureRealtimeUpdate
            {
                SourceDepotId = transfer.SourceDepotId,
                TargetDepotId = transfer.TargetDepotId,
                ClosureId = closure.Id,
                TransferId = transfer.Id,
                EntityType = "Closure",
                Action = closureAction,
                Status = closure.Status.ToString()
            },
            cancellationToken);

        await operationalHubService.PushDepotInventoryUpdateAsync(
            transfer.SourceDepotId,
            "ClosureTransferReceived",
            cancellationToken);
        await operationalHubService.PushDepotInventoryUpdateAsync(
            transfer.TargetDepotId,
            "ClosureTransferReceived",
            cancellationToken);

        return new ReceiveClosureTransferResponse
        {
            TransferId = transfer.Id,
            ClosureId = closure.Id,
            TransferStatus = transfer.Status,
            ClosureStatus = closure.Status.ToString(),
            ConsumableUnitsMoved = transfer.SnapshotConsumableUnits,
            ReusableItemsMoved = transfer.SnapshotReusableUnits,
            RequiresFurtherResolution = requiresFurtherResolution,
            RemainingItemCount = remainingItemCount,
            CompletedAt = completedAt,
            Message = requiresFurtherResolution
                ? "Đã xác nhận nhận hàng cho đợt chuyển kho này. Toàn bộ đợt chuyển kho đã khép lại nhưng kho nguồn vẫn còn hàng, admin cần chọn bước xử lý tiếp theo."
                : closure.CompletedAt.HasValue
                    ? "Đã xác nhận nhận hàng. Toàn bộ phần hàng cần xử lý bằng chuyển kho đã hoàn tất, kho nguồn chờ admin xác nhận đóng kho."
                    : "Đã xác nhận nhận hàng cho đợt chuyển kho này. Các đợt chuyển kho còn lại của phiên đóng kho vẫn tiếp tục được xử lý."
        };
    }
}
