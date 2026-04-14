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
/// Manager kho d�ch x�c nh?n nh?n h�ng.
/// Sau d� h? th?ng bulk-transfer inventory v� d�nh d?u phi�n x? l� h�ng t?n d� xong,
/// nhung v?n ch? admin g?i POST /logistics/depot/{id}/close d? d�ng kho th?t s?.
/// </summary>
public class ReceiveClosureTransferCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
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
            ?? throw new NotFoundException($"Kh�ng t�m th?y b?n ghi chuy?n kho #{request.TransferId}.");

        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw ExceptionCodes.WithCode(
                new BadRequestException("T�i kho?n kh�ng qu?n l� kho n�o dang ho?t d?ng."),
                LogisticsErrorCodes.DepotManagerNotAssigned);

        if (managerDepotId != transfer.TargetDepotId)
            throw new ForbiddenException("B?n kh�ng ph?i l� manager c?a kho d�ch trong qu� tr�nh nh?n h�ng n�y.");

        var closure = await closureRepository.GetByIdAsync(transfer.ClosureId, cancellationToken)
            ?? throw new NotFoundException($"Kh�ng t�m th?y b?n ghi d�ng kho #{transfer.ClosureId}.");

        var sourceDepot = await depotRepository.GetByIdAsync(transfer.SourceDepotId, cancellationToken)
            ?? throw new NotFoundException($"Kh�ng t�m th?y kho ngu?n #{transfer.SourceDepotId}.");

        var transferItems = await transferRepository.GetItemsByTransferIdAsync(transfer.Id, cancellationToken);
        if (transferItems.Count == 0)
            throw new ConflictException("Transfer kh�ng c� v?t ph?m du?c c?u h�nh d? nh?n h�ng.");

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
                closure.CompletedAt.HasValue ? "X? l� h�ng t?n d� ho�n t?t" : "�� ho�n t?t m?t d?t chuy?n kho",
                closure.CompletedAt.HasValue
                    ? $"To�n b? h�ng t?n c?a kho '{sourceDepot.Name}' d� du?c chuy?n xong theo k? ho?ch. Kho v?n ? tr?ng th�i Unavailable v� ch? admin x�c nh?n d�ng kho."
                    : $"Transfer #{transfer.Id} t? kho '{sourceDepot.Name}' d� du?c nh?n th�nh c�ng. V?n c�n c�c transfer kh�c ch? ho�n t?t.",
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
                ? "�� x�c nh?n nh?n h�ng. To�n b? k? ho?ch ph�n b? h�ng t?n d� ho�n t?t, kho ngu?n v?n gi? tr?ng th�i Unavailable v� ch? admin x�c nh?n d�ng kho."
                : "�� x�c nh?n nh?n h�ng cho transfer n�y. C�c transfer c�n l?i c?a phi�n d�ng kho v?n ti?p t?c du?c x? l�."
        };
    }
}

