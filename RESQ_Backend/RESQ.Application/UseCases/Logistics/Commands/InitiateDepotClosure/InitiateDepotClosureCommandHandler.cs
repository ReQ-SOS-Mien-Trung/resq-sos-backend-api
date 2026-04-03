using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;

public class InitiateDepotClosureCommandHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
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

        // 1. Load kho
        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ");

        // 2. Nếu kho đang Closing → trả về closure hiện tại (idempotent)
        if (depot.Status == DepotStatus.Closing)
        {
            var existingClosure = await closureRepository.GetActiveClosureByDepotIdAsync(request.DepotId, cancellationToken);
            if (existingClosure != null)
            {
                logger.LogInformation("InitiateDepotClosure idempotent — ClosureId={ClosureId}", existingClosure.Id);
                return BuildResponse(existingClosure, depot.Name, requiresResolution: true);
            }
        }

        // 3. Nếu đã Closed → không cho đóng lại
        if (depot.Status == DepotStatus.Closed)
            throw new ConflictException("Kho đã đóng cửa.");

        // 4. Guard: không phải kho duy nhất còn hoạt động
        var activeCount = await depotRepository.GetActiveDepotCountExcludingAsync(request.DepotId, cancellationToken);
        if (activeCount == 0)
            throw new ConflictException("Không thể đóng kho duy nhất còn đang hoạt động trong hệ thống.");

        // 5. Guard: còn yêu cầu tiếp tế đang xử lý
        var (asSource, asRequester) = await depotRepository.GetNonTerminalSupplyRequestCountsAsync(
            request.DepotId, cancellationToken);
        if (asSource > 0 || asRequester > 0)
        {
            var parts = new List<string>();
            if (asSource > 0)
                parts.Add($"{asSource} yêu cầu xuất hàng đi chưa xong");
            if (asRequester > 0)
                parts.Add($"{asRequester} yêu cầu nhận hàng về chưa xong");

            throw new ConflictException(
                $"Kho đang có {string.Join(" và ", parts)}. Hãy hoàn tất hoặc huỷ hết trước khi đóng kho.");
        }

        // 6. Lấy snapshot tồn kho
        var consumableVolume = await depotRepository.GetConsumableTransferVolumeAsync(request.DepotId, cancellationToken);
        var consumableRowCount = await depotRepository.GetConsumableInventoryRowCountAsync(request.DepotId, cancellationToken);
        var (reusableAvailable, reusableInUse) = await depotRepository.GetReusableItemCountsAsync(request.DepotId, cancellationToken);
        var totalReusable = reusableAvailable + reusableInUse;

        var previousStatus = depot.Status;

        // 7. Trường hợp kho trống → đóng ngay
        if (consumableVolume == 0 && totalReusable == 0)
        {
            DepotClosureRecord? completedClosure = null;
            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                depot.InitiateClosing();
                depot.CompleteClosing();
                await depotRepository.UpdateAsync(depot, cancellationToken);

                completedClosure = DepotClosureRecord.Create(
                    depotId: request.DepotId,
                    initiatedBy: request.InitiatedBy,
                    closeReason: request.Reason,
                    previousStatus: previousStatus,
                    snapshotConsumableUnits: 0,
                    snapshotReusableUnits: 0,
                    totalConsumableRows: 0,
                    totalReusableUnits: 0);
                completedClosure.Complete(DateTime.UtcNow);

                await closureRepository.CreateAsync(completedClosure, cancellationToken);
                await unitOfWork.SaveAsync();
            });

            logger.LogInformation("Depot {DepotId} closed immediately (empty inventory)", request.DepotId);
            return BuildResponse(completedClosure!, depot.Name, requiresResolution: false);
        }

        // 8. Kho có hàng → đặt Closing, tạo closure record
        DepotClosureRecord? closure = null;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            depot.InitiateClosing();
            await depotRepository.UpdateAsync(depot, cancellationToken);

            closure = DepotClosureRecord.Create(
                depotId: request.DepotId,
                initiatedBy: request.InitiatedBy,
                closeReason: request.Reason,
                previousStatus: previousStatus,
                snapshotConsumableUnits: consumableVolume,
                snapshotReusableUnits: totalReusable,
                totalConsumableRows: consumableRowCount,
                totalReusableUnits: totalReusable);

            await closureRepository.CreateAsync(closure, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "Depot {DepotId} moved to Closing | ClosureId={ClosureId} ConsumableUnits={Consumable} ReusableUnits={Reusable}",
            request.DepotId, closure!.Id, consumableVolume, totalReusable);

        var response = BuildResponse(closure, depot.Name, requiresResolution: true);
        response.InventorySummary.ConsumableItemTypeCount = consumableRowCount;
        response.InventorySummary.ConsumableUnitTotal = consumableVolume;
        response.InventorySummary.ReusableAvailableCount = reusableAvailable;
        response.InventorySummary.ReusableInUseCount = reusableInUse;
        return response;
    }

    private static InitiateDepotClosureResponse BuildResponse(
        DepotClosureRecord closure, string depotName, bool requiresResolution)
    {
        return new InitiateDepotClosureResponse
        {
            ClosureId = closure.Id,
            DepotId = closure.DepotId,
            DepotName = depotName,
            RequiresResolution = requiresResolution,
            TimeoutAt = requiresResolution ? closure.ClosingTimeoutAt : null,
            Message = requiresResolution
                ? "Kho đã được đặt sang trạng thái Closing. Vui lòng chọn cách xử lý hàng tồn kho."
                : "Kho không có hàng tồn — đã đóng thành công.",
            InventorySummary = new InventorySummaryDto
            {
                ConsumableUnitTotal = closure.SnapshotConsumableUnits,
                ReusableAvailableCount = closure.SnapshotReusableUnits
            }
        };
    }
}
