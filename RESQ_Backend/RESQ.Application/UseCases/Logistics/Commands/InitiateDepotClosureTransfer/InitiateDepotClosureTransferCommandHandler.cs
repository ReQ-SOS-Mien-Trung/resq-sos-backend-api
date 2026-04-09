using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosureTransfer;

/// <summary>
/// Admin chỉ định kho đích và khởi động luồng chuyển kho để đóng kho nguồn.
/// Tự động tạo DepotClosureRecord + DepotClosureTransferRecord trong một transaction.
/// </summary>
public class InitiateDepotClosureTransferCommandHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotClosureTransferRepository transferRepository,
    IDepotInventoryRepository inventoryRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork,
    ILogger<InitiateDepotClosureTransferCommandHandler> logger)
    : IRequestHandler<InitiateDepotClosureTransferCommand, InitiateDepotClosureTransferResponse>
{
    public async Task<InitiateDepotClosureTransferResponse> Handle(
        InitiateDepotClosureTransferCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "InitiateDepotClosureTransfer | SourceDepot={Src} TargetDepot={Tgt} By={By}",
            request.DepotId, request.TargetDepotId, request.InitiatedBy);

        // 1. Load kho nguồn, phải Unavailable
        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho nguồn.");

        if (depot.Status != DepotStatus.Unavailable)
            throw new ConflictException(
                $"Kho đang ở trạng thái '{depot.Status}'. Phải chuyển sang Unavailable trước khi chuyển hàng.");

        // 2. Guard: không phải kho duy nhất còn hoạt động
        var activeCount = await depotRepository.GetActiveDepotCountExcludingAsync(request.DepotId, cancellationToken);
        if (activeCount == 0)
            throw new ConflictException("Không thể đóng kho duy nhất còn đang hoạt động trong hệ thống.");

        // 3. Guard: không có phiên chuyển kho nào đang chạy cho kho này
        var existingClosure = await closureRepository.GetActiveClosureByDepotIdAsync(request.DepotId, cancellationToken);
        if (existingClosure != null)
            throw new ConflictException(
                "Kho đang có phiên chuyển kho chưa hoàn tất. Hủy phiên cũ trước khi tạo mới.");

        // 4. Load kho đích và validate trạng thái
        var targetDepot = await depotRepository.GetByIdAsync(request.TargetDepotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho đích.");

        if (targetDepot.Status is DepotStatus.Unavailable or DepotStatus.Closed)
            throw new ConflictException(
                $"Kho đích '{targetDepot.Name}' không khả dụng (trạng thái: {targetDepot.Status}). Vui lòng chọn kho khác.");

        // 5. Kiểm tra sức chứa kho đích
        var consumableVolume = await depotRepository.GetConsumableTransferVolumeAsync(request.DepotId, cancellationToken);
        var availableCapacity = targetDepot.Capacity - targetDepot.CurrentUtilization;
        if (consumableVolume > availableCapacity)
            throw new ConflictException(
                $"Kho đích '{targetDepot.Name}' không đủ sức chứa. " +
                $"Cần: {consumableVolume:N0} — Còn trống: {availableCapacity:N0} đơn vị.");

        // 6. Lấy snapshot tồn kho
        var consumableRowCount = await depotRepository.GetConsumableInventoryRowCountAsync(request.DepotId, cancellationToken);
        var (reusableAvailable, reusableInUse) = await depotRepository.GetReusableItemCountsAsync(request.DepotId, cancellationToken);

        // 7. Tạo ClosureRecord (audit) + TransferRecord trong một transaction
        var closure = DepotClosureRecord.Create(
            depotId: request.DepotId,
            initiatedBy: request.InitiatedBy,
            closeReason: request.Reason,
            previousStatus: depot.Status,
            snapshotConsumableUnits: consumableVolume,
            snapshotReusableUnits: reusableAvailable + reusableInUse,
            totalConsumableRows: consumableRowCount,
            totalReusableUnits: reusableAvailable + reusableInUse);
        closure.SetTransferResolution(request.TargetDepotId);

        DepotClosureTransferRecord transfer = null!;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var closureId = await closureRepository.CreateAsync(closure, cancellationToken);
            closure.SetGeneratedId(closureId);

            transfer = DepotClosureTransferRecord.Create(
                closureId: closureId,
                sourceDepotId: request.DepotId,
                targetDepotId: request.TargetDepotId,
                snapshotConsumableUnits: consumableVolume,
                snapshotReusableUnits: reusableAvailable);

            closure.MarkTransferPending();
            await transferRepository.CreateAsync(transfer, cancellationToken);
            await closureRepository.UpdateAsync(closure, cancellationToken);
        });

        logger.LogInformation(
            "DepotClosureTransfer created | ClosureId={C} TransferId={T}",
            closure.Id, transfer.Id);

        // 8. Gửi thông báo cho manager kho đích
        try
        {
            var targetManagerId = await inventoryRepository.GetActiveManagerUserIdByDepotIdAsync(
                request.TargetDepotId, cancellationToken);
            if (targetManagerId.HasValue)
                await firebaseService.SendNotificationToUserAsync(
                    targetManagerId.Value,
                    "Kho của bạn sắp tiếp nhận hàng chuyển kho",
                    $"Admin đã chỉ định '{targetDepot.Name}' tiếp nhận hàng từ kho '{depot.Name}' đang đóng cửa. " +
                    $"Cần chuẩn bị {consumableVolume:N0} đơn vị tiêu hao.",
                    "depot_closure_transfer_assigned",
                    new Dictionary<string, string>
                    {
                        ["sourceDepotId"] = request.DepotId.ToString(),
                        ["transferId"]    = transfer.Id.ToString()
                    },
                    cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify target manager | TransferId={Id}", transfer.Id);
        }

        return new InitiateDepotClosureTransferResponse
        {
            TransferId              = transfer.Id,
            SourceDepotId           = request.DepotId,
            SourceDepotName         = depot.Name,
            TargetDepotId           = request.TargetDepotId,
            TargetDepotName         = targetDepot.Name,
            TransferStatus          = transfer.Status,
            SnapshotConsumableUnits = consumableVolume,
            SnapshotReusableUnits   = reusableAvailable,
            ReusableItemsSkipped    = reusableInUse,
            Message = $"Đã xác nhận kho đích '{targetDepot.Name}'. " +
                      "Manager kho nguồn vui lòng chuẩn bị và xuất hàng. " +
                      "Manager kho đích xác nhận nhận hàng để hoàn tất đóng kho."
        };
    }
}
