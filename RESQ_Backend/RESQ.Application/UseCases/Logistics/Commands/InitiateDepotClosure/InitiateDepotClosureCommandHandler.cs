using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;

public class InitiateDepotClosureCommandHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotFundDrainService depotFundDrainService,
    IUnitOfWork unitOfWork,
    ILogger<InitiateDepotClosureCommandHandler> logger)
    : IRequestHandler<InitiateDepotClosureCommand, InitiateDepotClosureResponse>
{
    public async Task<InitiateDepotClosureResponse> Handle(
        InitiateDepotClosureCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "InitiateDepotClosure | DepotId={DepotId} InitiatedBy={InitiatedBy}",
            request.DepotId, request.InitiatedBy);

        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException("KhÃ´ng tÃ¬m tháº¥y kho cá»©u trá»£.");

        if (depot.Status == DepotStatus.Closed)
            throw new ConflictException("Kho Ä‘Ã£ Ä‘Ã³ng cá»­a.");

        if (depot.Status != DepotStatus.Closing)
        {
            throw new ConflictException(
                $"Kho Ä‘ang á»Ÿ tráº¡ng thÃ¡i '{depot.Status}'. " +
                "Admin pháº£i chuyá»ƒn kho sang Unavailable trÆ°á»›c khi Ä‘Ã³ng kho.");
        }

        var activeCount = await depotRepository.GetActiveDepotCountExcludingAsync(request.DepotId, cancellationToken);
        if (activeCount == 0)
            throw new ConflictException("KhÃ´ng thá»ƒ Ä‘Ã³ng kho duy nháº¥t cÃ²n Ä‘ang hoáº¡t Ä‘á»™ng trong há»‡ thá»‘ng.");

        var latestClosure = await closureRepository.GetLatestClosureByDepotIdAsync(request.DepotId, cancellationToken);

        var inventoryItems = await depotRepository.GetDetailedInventoryForClosureAsync(request.DepotId, cancellationToken);
        if (inventoryItems.Count > 0)
        {
            var totalConsumable = inventoryItems
                .Where(i => i.ItemType == "Consumable")
                .Sum(i => i.Quantity);
            var totalReusable = inventoryItems
                .Where(i => i.ItemType == "Reusable")
                .Sum(i => i.Quantity);

            return new InitiateDepotClosureResponse
            {
                DepotId = depot.Id,
                DepotName = depot.Name,
                Success = false,
                Message = $"Kho váº«n cÃ²n hÃ ng tá»“n ({totalConsumable} Ä‘Æ¡n vá»‹ tiÃªu hao, {totalReusable} thiáº¿t bá»‹ tÃ¡i sá»­ dá»¥ng). " +
                          "HÃ£y chá»n cÃ¡ch xá»­ lÃ½: chuyá»ƒn kho (POST /close/transfer) hoáº·c xá»­ lÃ½ bÃªn ngoÃ i (POST /close/external-resolution).",
                RemainingItems = inventoryItems
            };
        }

        if (latestClosure is
            {
                Status: not DepotClosureStatus.Completed
                    and not DepotClosureStatus.Cancelled
                    and not DepotClosureStatus.Failed
            })
        {
            throw new ConflictException(
                $"PhiÃªn Ä‘Ã³ng kho hiá»‡n táº¡i Ä‘ang á»Ÿ tráº¡ng thÃ¡i '{latestClosure.Status}'. " +
                "Cáº§n hoÃ n táº¥t xá»­ lÃ½ hÃ ng tá»“n trÆ°á»›c khi admin xÃ¡c nháº­n Ä‘Ã³ng kho.");
        }

        var previousStatus = depot.Status;
        DepotClosureRecord? closureRecord = null;
        var isFinalizingExistingClosure = latestClosure?.Status == DepotClosureStatus.Completed;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            depot.CompleteClosing();
            await depotRepository.UpdateAsync(depot, cancellationToken);

            if (isFinalizingExistingClosure)
            {
                closureRecord = latestClosure!;
            }
            else
            {
                closureRecord = DepotClosureRecord.Create(
                    depotId: request.DepotId,
                    initiatedBy: request.InitiatedBy,
                    closeReason: request.Reason,
                    previousStatus: previousStatus,
                    snapshotConsumableUnits: 0,
                    snapshotReusableUnits: 0,
                    totalConsumableRows: 0,
                    totalReusableUnits: 0);
                closureRecord.Complete(DateTime.UtcNow);

                var id = await closureRepository.CreateAsync(closureRecord, cancellationToken);
                closureRecord.SetGeneratedId(id);
            }

            await depotFundDrainService.DrainAllToSystemFundAsync(
                request.DepotId,
                closureRecord!.Id,
                request.InitiatedBy,
                cancellationToken);

            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "Depot {DepotId} closed successfully | FinalizedExistingClosure={FinalizedExistingClosure} ClosureId={ClosureId}",
            request.DepotId,
            isFinalizingExistingClosure,
            closureRecord!.Id);

        return new InitiateDepotClosureResponse
        {
            DepotId = depot.Id,
            DepotName = depot.Name,
            ClosureId = closureRecord!.Id,
            Success = true,
            Message = isFinalizingExistingClosure
                ? "ÄÃ£ xÃ¡c nháº­n hoÃ n táº¥t Ä‘Ã³ng kho. Quá»¹ kho Ä‘Ã£ Ä‘Æ°á»£c chuyá»ƒn vá» quá»¹ há»‡ thá»‘ng, kho chuyá»ƒn sang Closed vÃ  manager Ä‘Ã£ Ä‘Æ°á»£c gá»¡."
                : "Kho khÃ´ng cÃ³ hÃ ng tá»“n nÃªn Ä‘Ã£ Ä‘Æ°á»£c Ä‘Ã³ng ngay. Quá»¹ kho Ä‘Ã£ Ä‘Æ°á»£c chuyá»ƒn vá» quá»¹ há»‡ thá»‘ng vÃ  manager Ä‘Ã£ Ä‘Æ°á»£c gá»¡."
        };
    }
}


