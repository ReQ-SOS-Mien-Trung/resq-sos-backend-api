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
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        if (depot.Status == DepotStatus.Closed)
            throw new ConflictException("Kho đã đóng cửa.");

        if (depot.Status != DepotStatus.Unavailable)
        {
            throw new ConflictException(
                $"Kho đang ở trạng thái '{depot.Status}'. " +
                "Admin phải chuyển kho sang Unavailable trước khi đóng kho.");
        }

        var activeCount = await depotRepository.GetActiveDepotCountExcludingAsync(request.DepotId, cancellationToken);
        if (activeCount == 0)
            throw new ConflictException("Không thể đóng kho duy nhất còn đang hoạt động trong hệ thống.");

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
                Message = $"Kho vẫn còn hàng tồn ({totalConsumable} đơn vị tiêu hao, {totalReusable} thiết bị tái sử dụng). " +
                          "Hãy chọn cách xử lý: chuyển kho (POST /close/transfer) hoặc xử lý bên ngoài (POST /close/external-resolution).",
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
                $"Phiên đóng kho hiện tại đang ở trạng thái '{latestClosure.Status}'. " +
                "Cần hoàn tất xử lý hàng tồn trước khi admin xác nhận đóng kho.");
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
                ? "Đã xác nhận hoàn tất đóng kho. Quỹ kho đã được chuyển về quỹ hệ thống, kho chuyển sang Closed và manager đã được gỡ."
                : "Kho không có hàng tồn nên đã được đóng ngay. Quỹ kho đã được chuyển về quỹ hệ thống và manager đã được gỡ."
        };
    }
}

