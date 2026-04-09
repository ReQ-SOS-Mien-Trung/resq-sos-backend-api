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

        // 1. Load kho
        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        // 2. Nếu đã Closed → không cho đóng lại
        if (depot.Status == DepotStatus.Closed)
            throw new ConflictException("Kho đã đóng cửa.");

        // 3. Phải ở trạng thái Unavailable trước khi đóng
        if (depot.Status != DepotStatus.Unavailable)
            throw new ConflictException(
                $"Kho đang ở trạng thái '{depot.Status}'. " +
                "Admin phải chuyển kho sang Unavailable trước khi đóng kho.");

        // 4. Guard: không phải kho duy nhất còn hoạt động
        var activeCount = await depotRepository.GetActiveDepotCountExcludingAsync(request.DepotId, cancellationToken);
        if (activeCount == 0)
            throw new ConflictException("Không thể đóng kho duy nhất còn đang hoạt động trong hệ thống.");

        // 5. Kiểm tra tồn kho — nếu còn hàng thì trả 409 kèm chi tiết (không tạo closure record)
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

        // 6. Kho trống → đóng ngay
        var previousStatus = depot.Status;
        DepotClosureRecord? closureRecord = null;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            depot.CompleteClosing();
            await depotRepository.UpdateAsync(depot, cancellationToken);

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

            // Drain quỹ kho (balance > 0) về quỹ hệ thống
            await depotFundDrainService.DrainAllToSystemFundAsync(
                request.DepotId, id, request.InitiatedBy, cancellationToken);
        });

        logger.LogInformation("Depot {DepotId} closed successfully (empty inventory)", request.DepotId);

        return new InitiateDepotClosureResponse
        {
            DepotId = depot.Id,
            DepotName = depot.Name,
            ClosureId = closureRecord!.Id,
            Success = true,
            Message = "Kho không có hàng tồn — đã đóng thành công."
        };
    }
}
