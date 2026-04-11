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
            ?? throw new NotFoundException("Không tìm th?y kho c?u tr?.");

        if (depot.Status == DepotStatus.Closed)
            throw new ConflictException("Kho da dong cua.");

        if (depot.Status != DepotStatus.Unavailable)
        {
            throw new ConflictException(
                $"Kho dang o trang thai '{depot.Status}'. " +
                "Admin phai chuyen kho sang Unavailable truoc khi dong kho.");
        }

        var activeCount = await depotRepository.GetActiveDepotCountExcludingAsync(request.DepotId, cancellationToken);
        if (activeCount == 0)
            throw new ConflictException("Không th? dóng kho duy nh?t còn dang ho?t d?ng trong h? th?ng.");

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
                Message = $"Kho van con hang ton ({totalConsumable} don vi tieu hao, {totalReusable} thiet bi tai su dung). " +
                          "Hay chon cach xu ly: chuyen kho (POST /close/transfer) hoac xu ly ben ngoai (POST /close/external-resolution).",
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
                $"Phien dong kho hien tai dang o trang thai '{latestClosure.Status}'. " +
                "Can hoan tat xu ly hang ton truoc khi admin xac nhan dong kho.");
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
                ? "Ðã xác nh?n hoàn t?t dóng kho. Qu? kho dã du?c chuy?n v? qu? h? th?ng, kho chuy?n sang Closed và manager dã du?c g?."
                : "Kho không có hàng t?n nên dã du?c dóng ngay. Qu? kho dã du?c chuy?n v? qu? h? th?ng và manager dã du?c g?."
        };
    }
}
