using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.ReceiveClosureTransfer;

/// <summary>
/// Quản lý kho đích xác nhận nhận hàng → Received.
/// Sau đó kích hoạt BulkTransferForClosure và hoàn tất bản ghi đóng kho + depot.
/// </summary>
public class ReceiveClosureTransferCommandHandler(
    IDepotClosureTransferRepository transferRepository,
    IDepotClosureRepository closureRepository,
    IDepotRepository depotRepository,
    IDepotInventoryRepository inventoryRepository,
    IDepotFundDrainService depotFundDrainService,
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

        // Kiểm tra người thực hiện là manager của kho đích
        var managerDepotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != transfer.TargetDepotId)
            throw new ForbiddenException("Bạn không phải manager của kho đích trong quá trình nhận hàng này.");

        var closure = await closureRepository.GetByIdAsync(transfer.ClosureId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi đóng kho #{transfer.ClosureId}.");

        var sourceDepot = await depotRepository.GetByIdAsync(transfer.SourceDepotId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy kho nguồn #{transfer.SourceDepotId}.");

        // Transition: Completed → Received (domain validates)
        transfer.MarkReceived(request.UserId, request.Note);
        var completedAt = DateTime.UtcNow;

        // Thực hiện bulk transfer thực tế (di chuyển inventory)
        var (processedRows, _) = await inventoryRepository.BulkTransferForClosureAsync(
            sourceDepotId: transfer.SourceDepotId,
            targetDepotId: transfer.TargetDepotId,
            closureId: transfer.ClosureId,
            performedBy: request.UserId,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "BulkTransfer completed | ClosureId={C} Rows={R} TransferId={T}",
            transfer.ClosureId, processedRows, transfer.Id);

        // Cập nhật closure + depot trong transaction
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Drain quỹ kho nguồn (balance > 0) về quỹ hệ thống
            await depotFundDrainService.DrainAllToSystemFundAsync(
                transfer.SourceDepotId, transfer.ClosureId, request.UserId, cancellationToken);

            sourceDepot.CompleteClosing();
            closure.Complete(completedAt);

            await transferRepository.UpdateAsync(transfer, cancellationToken);
            await depotRepository.UpdateAsync(sourceDepot, cancellationToken);
            await closureRepository.UpdateAsync(closure, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "DepotClosure completed via transfer | DepotId={D} ClosureId={C}",
            transfer.SourceDepotId, closure.Id);

        // Notify admin who initiated closure
        try
        {
            await firebaseService.SendNotificationToUserAsync(
                closure.InitiatedBy,
                "Đóng kho hoàn tất",
                $"Kho đã được đóng thành công. Toàn bộ hàng tồn đã được chuyển sang kho đích.",
                "depot_closure_completed",
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify admin | ClosureId={Id}", closure.Id);
        }

        return new ReceiveClosureTransferResponse
        {
            TransferId = transfer.Id,
            ClosureId = closure.Id,
            TransferStatus = transfer.Status,
            ConsumableUnitsMoved = transfer.SnapshotConsumableUnits,
            ReusableItemsMoved = transfer.SnapshotReusableUnits,
            CompletedAt = completedAt,
            Message = "Đã xác nhận nhận hàng. Quá trình đóng kho đã hoàn tất."
        };
    }
}
