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
/// Manager kho dich xac nhan nhan hang.
/// Sau do he thong bulk-transfer inventory va danh dau phien xu ly hang ton da xong,
/// nhung van cho admin goi POST /logistics/depot/{id}/close de dong kho that su.
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
            ?? throw new NotFoundException($"Khong tim thay ban ghi chuyen kho #{request.TransferId}.");

        var managerDepotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw ExceptionCodes.WithCode(
                new BadRequestException("Tai khoan khong quan ly kho nao dang hoat dong."),
                LogisticsErrorCodes.DepotManagerNotAssigned);

        if (managerDepotId != transfer.TargetDepotId)
            throw new ForbiddenException("Ban khong phai manager cua kho dich trong qua trinh nhan hang nay.");

        var closure = await closureRepository.GetByIdAsync(transfer.ClosureId, cancellationToken)
            ?? throw new NotFoundException($"Khong tim thay ban ghi dong kho #{transfer.ClosureId}.");

        var sourceDepot = await depotRepository.GetByIdAsync(transfer.SourceDepotId, cancellationToken)
            ?? throw new NotFoundException($"Khong tim thay kho nguon #{transfer.SourceDepotId}.");

        transfer.MarkReceived(request.UserId, request.Note);
        var completedAt = DateTime.UtcNow;

        var (processedRows, _) = await inventoryRepository.BulkTransferForClosureAsync(
            sourceDepotId: transfer.SourceDepotId,
            targetDepotId: transfer.TargetDepotId,
            closureId: transfer.ClosureId,
            performedBy: request.UserId,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "BulkTransfer completed | ClosureId={ClosureId} Rows={Rows} TransferId={TransferId}",
            transfer.ClosureId, processedRows, transfer.Id);

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            closure.Complete(completedAt);

            await transferRepository.UpdateAsync(transfer, cancellationToken);
            await closureRepository.UpdateAsync(closure, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "Depot closure inventory resolution completed via transfer | DepotId={DepotId} ClosureId={ClosureId}",
            transfer.SourceDepotId, closure.Id);

        try
        {
            await firebaseService.SendNotificationToUserAsync(
                closure.InitiatedBy,
                "Xu ly hang ton da hoan tat",
                $"Toan bo hang ton cua kho '{sourceDepot.Name}' da duoc chuyen sang kho dich. Kho van o trang thai Unavailable va cho admin xac nhan dong kho.",
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
            Message = "Da xac nhan nhan hang. Hang ton da duoc chuyen xong, kho nguon van giu trang thai Unavailable va cho admin xac nhan dong kho."
        };
    }
}
