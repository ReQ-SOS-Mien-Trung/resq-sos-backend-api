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
            ?? throw new NotFoundException("Khong tim thay ban ghi dong kho.");

        if (closure.DepotId != request.DepotId)
            throw new ConflictException("Ban ghi dong kho khong thuoc kho nay.");

        if (closure.Status != DepotClosureStatus.InProgress)
            throw new ConflictException(
                $"Ban ghi dong kho da o trang thai '{closure.Status}' - khong the tiep tuc xu ly. " +
                "Neu da het han, vui long tao yeu cau dong kho moi.");

        var claimed = await closureRepository.TryClaimForProcessingAsync(request.ClosureId, cancellationToken);
        if (!claimed)
            throw new ConflictException("Yeu cau dong kho dang duoc xu ly boi tien trinh khac. Vui long thu lai sau.");

        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Khong tim thay kho cuu tro.");

        if (depot.Status != DepotStatus.Closing)
            throw new ConflictException("Kho khong o trang thai Closing - khong the tiep tuc dong kho.");

        try
        {
            if (request.ResolutionType == CloseResolutionType.TransferToDepot)
                return await HandleTransferResolutionAsync(request, closure, depot, cancellationToken);
            else
                return await HandleExternalResolutionAsync(request, closure, depot, DateTime.UtcNow, cancellationToken);
        }
        catch (Exception ex) when (ex is not ConflictException and not NotFoundException)
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
            ?? throw new NotFoundException("Khong tim thay kho dich.");

        if (targetDepot.Status == DepotStatus.Closing || targetDepot.Status == DepotStatus.Closed)
            throw new ConflictException($"Kho dich '{targetDepot.Name}' dang dong hoac da dong.");

        var consumableVolume = await depotRepository.GetConsumableTransferVolumeAsync(request.DepotId, cancellationToken);
        var availableCapacity = targetDepot.Capacity - targetDepot.CurrentUtilization;
        if (consumableVolume > availableCapacity)
            throw new ConflictException(
                $"Kho dich '{targetDepot.Name}' khong du suc chua. " +
                $"Can: {consumableVolume:N0} - Con trong: {availableCapacity:N0} don vi.");

        var (reusableAvailable, reusableInUse) = await depotRepository.GetReusableItemCountsAsync(request.DepotId, cancellationToken);
        closure.RecordActualInventory(consumableVolume, reusableAvailable + reusableInUse);
        closure.SetTransferResolution(request.TargetDepotId!.Value);

        DepotClosureTransferRecord transfer = null!;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            transfer = DepotClosureTransferRecord.Create(
                closureId: closure.Id,
                sourceDepotId: request.DepotId,
                targetDepotId: request.TargetDepotId!.Value,
                snapshotConsumableUnits: consumableVolume,
                snapshotReusableUnits: reusableAvailable);

            await transferRepository.CreateAsync(transfer, cancellationToken);
            closure.ResetToInProgress();
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
                    "Kho cua ban sap tiep nhan hang chuyen kho",
                    $"Admin da chi dinh '{targetDepot.Name}' tiep nhan hang tu kho '{depot.Name}' dang dong cua. Can chuan bi {consumableVolume:N0} don vi tieu hao.",
                    "depot_closure_transfer_assigned",
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
            Message = $"Da xac nhan kho dich '{targetDepot.Name}'. Quan ly kho nguon vui long xac nhan xuat hang, sau do quan ly kho dich xac nhan nhan hang.",
            TransferSummary = new TransferSummaryDto
            {
                TransferId = transfer.Id,
                TargetDepotId = targetDepot.Id,
                TargetDepotName = targetDepot.Name,
                TransferStatus = transfer.Status,
                TransferDeadlineAt = transfer.TransferDeadlineAt,
                SnapshotConsumableUnits = consumableVolume,
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

        closure.RecordActualInventory(currentConsumable, reusableAvailable + reusableInUse);
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
            Message = "Dong kho thanh cong. Hang ton da duoc xu ly theo hinh thuc ben ngoai."
        };
    }
}
