using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Exceptions;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ResolveDepotClosure;

public class ResolveDepotClosureCommandHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotClosureTransferRepository transferRepository,
    IDepotInventoryRepository inventoryRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork,
    ILogger<ResolveDepotClosureCommandHandler> logger)
    : IRequestHandler<ResolveDepotClosureCommand, ResolveDepotClosureResponse>
{
    public async Task<ResolveDepotClosureResponse> Handle(
        ResolveDepotClosureCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "ResolveDepotClosure | ClosureId={ClosureId} DepotId={DepotId} ResolutionType={Type} PerformedBy={By}",
            request.ClosureId, request.DepotId, request.ResolutionType, request.PerformedBy);

        var closure = await closureRepository.GetByIdAsync(request.ClosureId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bản ghi đóng kho.");

        if (closure.DepotId != request.DepotId)
            throw new ConflictException("Bản ghi đóng kho không thuộc kho này.");

        // Kiểm tra trạng thái: InProgress = bình thường, Processing = có thể đang bị kẹt từ lần thử trước
        if (closure.Status == DepotClosureStatus.Processing)
        {
            // Thử tái claim bằng optimistic concurrency (dùng rowVersion đảm bảo atomic)
            // Nếu 2 request cùng cố recover, chỉ đúng 1 cái thành công nhờ row_version check
            var reclaimed = await closureRepository.TryForceClaimFromProcessingAsync(
                request.ClosureId, closure.RowVersion, cancellationToken);
            if (!reclaimed)
                throw new ConflictException("Yêu cầu đóng kho đang được xử lý bởi tiến trình khác. Vui lòng thử lại sau.");

            logger.LogInformation(
                "ResolveDepotClosure recovered stuck Processing | ClosureId={ClosureId}", request.ClosureId);
        }
        else if (closure.Status != DepotClosureStatus.InProgress)
        {
            throw new ConflictException(
                $"Bản ghi đóng kho đang ở trạng thái '{closure.Status}' — không thể tiếp tục xử lý. " +
                "Nếu đã hết hạn, vui lòng tạo yêu cầu đóng kho mới.");
        }
        else
        {
            var claimed = await closureRepository.TryClaimForProcessingAsync(request.ClosureId, cancellationToken);
            if (!claimed)
                throw new ConflictException("Yêu cầu đóng kho đang được xử lý bởi tiến trình khác. Vui lòng thử lại sau.");
        }

        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        if (depot.Status != DepotStatus.Unavailable)
            throw new ConflictException("Kho không ở trạng thái Unavailable — không thể tiếp tục đóng kho.");

        try
        {
            if (request.ResolutionType == CloseResolutionType.TransferToDepot)
                return await HandleTransferResolutionAsync(request, closure, depot, cancellationToken);
            else
                return await HandleExternalResolutionAsync(request, closure, depot, DateTime.UtcNow, cancellationToken);
        }
        catch (Exception ex) when (ex is ConflictException or NotFoundException or BadRequestException)
        {
            // Lỗi validation nghiệp vụ — hoàn tác claim để user có thể thử lại
            await closureRepository.ResetProcessingToInProgressAsync(request.ClosureId, cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ResolveDepotClosure failed | ClosureId={ClosureId}", request.ClosureId);
            closure.RecordFailure(ex.Message);
            await closureRepository.UpdateAsync(closure, cancellationToken);
            await unitOfWork.SaveAsync();
            if (closure.Status == DepotClosureStatus.Failed)
            {
                depot.RestoreFromClosing(closure.PreviousStatus);
                await depotRepository.UpdateAsync(depot, cancellationToken);
                await unitOfWork.SaveAsync();
            }
            throw;
        }
    }

    private async Task<ResolveDepotClosureResponse> HandleTransferResolutionAsync(
        ResolveDepotClosureCommand request,
        DepotClosureRecord closure,
        DepotModel depot,
        CancellationToken cancellationToken)
    {
        var targetDepot = await depotRepository.GetByIdAsync(request.TargetDepotId!.Value, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho đích.");

        if (targetDepot.Status is DepotStatus.Unavailable or DepotStatus.Closed)
            throw new ConflictException($"Kho đích '{targetDepot.Name}' không khả dụng (trạng thái: {targetDepot.Status}). Vui lòng chọn kho khác.");

        var consumableVolume = await depotRepository.GetConsumableTransferVolumeAsync(request.DepotId, cancellationToken);
        var availableVolumeCapacity = targetDepot.Capacity - targetDepot.CurrentUtilization;
        if (consumableVolume > availableVolumeCapacity)
            throw new ConflictException(
                $"Kho đích '{targetDepot.Name}' không đủ sức chứa thể tích. " +
                $"Cần: {consumableVolume:N0} — Còn trống: {availableVolumeCapacity:N0} dm³.");

        var (reusableAvailable, reusableInUse) = await depotRepository.GetReusableItemCountsAsync(request.DepotId, cancellationToken);
        closure.RecordActualInventory((int)consumableVolume, reusableAvailable + reusableInUse);
        closure.SetTransferResolution(request.TargetDepotId!.Value);

        DepotClosureTransferRecord transfer = null!;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            transfer = DepotClosureTransferRecord.Create(
                closureId: closure.Id,
                sourceDepotId: request.DepotId,
                targetDepotId: request.TargetDepotId!.Value,
                snapshotConsumableUnits: (int)consumableVolume,
                snapshotReusableUnits: reusableAvailable);

            await transferRepository.CreateAsync(transfer, cancellationToken);
            closure.MarkTransferPending();
            await closureRepository.UpdateAsync(closure, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation("DepotClosureTransfer created | ClosureId={C} TransferId={T}", closure.Id, transfer.Id);

        try
        {
            var targetManagerId = await inventoryRepository.GetActiveManagerUserIdByDepotIdAsync(request.TargetDepotId!.Value, cancellationToken);
            if (targetManagerId.HasValue)
                await firebaseService.SendNotificationToUserAsync(
                    targetManagerId.Value,
                    "Kho của bạn sắp tiếp nhận hàng chuyển kho",
                    $"Admin đã chỉ định '{targetDepot.Name}' tiếp nhận hàng từ kho '{depot.Name}' đang đóng cửa. Cần chuẩn bị {consumableVolume:N0} đơn vị tiêu hao.",
                    "depot_closure_transfer_assigned",
                    // Deep-link params — mobile app dùng để navigate thẳng đến màn hình xác nhận nhận hàng.
                    // sourceDepotId = {id} trong route /logistics/depot/{id}/close/{closureId}/transfer/{transferId}/receive
                    new Dictionary<string, string>
                    {
                        ["sourceDepotId"] = request.DepotId.ToString(),
                        ["closureId"]     = closure.Id.ToString(),
                        ["transferId"]    = transfer.Id.ToString()
                    },
                    cancellationToken);
        }
        catch (Exception notifyEx) { logger.LogWarning(notifyEx, "Failed to notify target manager, TransferId={Id}", transfer.Id); }

        return new ResolveDepotClosureResponse
        {
            ClosureId = closure.Id,
            DepotId = request.DepotId,
            DepotName = depot.Name,
            ResolutionType = request.ResolutionType.ToString(),
            TransferPending = true,
            TransferId = transfer.Id,
            Message = $"Đã xác nhận kho đích '{targetDepot.Name}'. Quản lý kho nguồn vui lòng xác nhận xuất hàng, sau đó quản lý kho đích xác nhận nhận hàng.",
            TransferSummary = new TransferSummaryDto
            {
                TransferId = transfer.Id,
                TargetDepotId = targetDepot.Id,
                TargetDepotName = targetDepot.Name,
                TransferStatus = transfer.Status,
                SnapshotConsumableUnits = (int)consumableVolume,
                SnapshotReusableUnits = reusableAvailable,
                ReusableItemsSkipped = reusableInUse
            }
        };
    }

    private async Task<ResolveDepotClosureResponse> HandleExternalResolutionAsync(
        ResolveDepotClosureCommand request,
        DepotClosureRecord closure,
        DepotModel depot,
        DateTime completedAt,
        CancellationToken cancellationToken)
    {
        var currentConsumable = await depotRepository.GetConsumableTransferVolumeAsync(request.DepotId, cancellationToken);
        var (reusableAvailable, reusableInUse) = await depotRepository.GetReusableItemCountsAsync(request.DepotId, cancellationToken);

        closure.RecordActualInventory((int)currentConsumable, reusableAvailable + reusableInUse);
        closure.SetExternalResolution(request.ExternalNote);

        await inventoryRepository.ZeroOutForClosureAsync(
            depotId: request.DepotId, closureId: closure.Id,
            performedBy: request.PerformedBy,
            note: request.ExternalNote,
            cancellationToken: cancellationToken);

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            depot.CompleteClosing();
            closure.Complete(completedAt);
            await depotRepository.UpdateAsync(depot, cancellationToken);
            await closureRepository.UpdateAsync(closure, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        return new ResolveDepotClosureResponse
        {
            ClosureId = closure.Id,
            DepotId = request.DepotId,
            DepotName = depot.Name,
            ResolutionType = request.ResolutionType.ToString(),
            CompletedAt = completedAt,
            Message = "Đóng kho thành công. Hàng tồn đã được xử lý theo hình thức bên ngoài."
        };
    }
}
