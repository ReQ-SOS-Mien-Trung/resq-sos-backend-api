using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;

public class InitiateDepotClosureCommandHandler(
    IManagerDepotAccessService managerDepotAccessService,
    IDepotRepository depotRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotClosureRepository closureRepository,
    IDepotClosureTransferRepository transferRepository,
    IDepotFundDrainService depotFundDrainService,
    IUserPermissionResolver permissionResolver,
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
            request.DepotId,
            request.InitiatedBy);

        var userPermissions = await permissionResolver.GetEffectivePermissionCodesAsync(
            request.InitiatedBy,
            cancellationToken);

        var isAdmin = userPermissions.Contains(
            PermissionConstants.InventoryGlobalManage,
            StringComparer.OrdinalIgnoreCase);

        if (!isAdmin)
        {
            var managedDepotId = await managerDepotAccessService.ResolveAccessibleDepotIdAsync(
                request.InitiatedBy,
                request.DepotId,
                cancellationToken);

            if (!managedDepotId.HasValue)
            {
                throw ExceptionCodes.WithCode(
                    new ForbiddenException("Tài khoản quản lý kho chưa được gán kho phụ trách."),
                    LogisticsErrorCodes.DepotManagerNotAssigned);
            }

            if (managedDepotId.Value != request.DepotId)
                throw new ForbiddenException("Bạn chỉ có thể xác nhận đóng kho mình đang quản lý.");
        }

        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        if (depot.Status == DepotStatus.Closed)
            throw new ConflictException("Kho đã đóng vĩnh viễn.");

        if (depot.Status != DepotStatus.Closing)
        {
            throw new ConflictException(
                $"Kho đang ở trạng thái '{depot.Status}'. Kho phải được chuyển sang Closing trước khi xác nhận đóng hoàn toàn.");
        }

        var activeCount = await depotRepository.GetActiveDepotCountExcludingAsync(request.DepotId, cancellationToken);
        if (activeCount == 0)
        {
            throw new ConflictException("Không thể đóng kho duy nhất còn đang hoạt động trong hệ thống.");
        }

        var latestClosure = await closureRepository.GetLatestClosureByDepotIdAsync(request.DepotId, cancellationToken);
        if (latestClosure == null)
        {
            throw new ConflictException(
                "Không tìm thấy phiên đóng kho hợp lệ cho kho này. Vui lòng thực hiện lại bước chuyển kho sang trạng thái Closing.");
        }

        if (latestClosure.Status == DepotClosureStatus.TransferPending)
        {
            var hasOpenTransfers = await transferRepository.HasOpenTransfersAsync(latestClosure.Id, cancellationToken);
            if (!hasOpenTransfers)
            {
                latestClosure.ReopenForResidualHandling();
                await closureRepository.UpdateAsync(latestClosure, cancellationToken);
                await unitOfWork.SaveAsync();
            }
        }

        var closingBlockers = await depotInventoryRepository.GetDepotClosingBlockersAsync(request.DepotId, cancellationToken);
        if (closingBlockers.HasAnyBlockingItems)
        {
            throw new ConflictException(BuildClosingBlockersMessage(closingBlockers));
        }

        var inventoryItems = await depotRepository.GetDetailedInventoryForClosureAsync(request.DepotId, cancellationToken);
        if (inventoryItems.Count > 0)
        {
            if (latestClosure.Status == DepotClosureStatus.Processing)
            {
                throw new ConflictException(
                    "Phiên đóng kho hiện tại đang được xử lý bởi tiến trình khác. Vui lòng thử lại sau.");
            }

            if (latestClosure.Status == DepotClosureStatus.TransferPending)
            {
                throw new ConflictException(
                    "Kho đang có phiên chuyển kho chưa hoàn tất. Vui lòng hoàn tất hoặc hủy phiên hiện tại trước khi xác nhận đóng kho.");
            }

            if (latestClosure.Status == DepotClosureStatus.Completed)
            {
                throw new ConflictException(
                    "Phiên đóng kho đã được đánh dấu xử lý xong hàng tồn nhưng kho vẫn còn dữ liệu tồn. Vui lòng kiểm tra lại dữ liệu.");
            }

            if (latestClosure.Status != DepotClosureStatus.InProgress)
            {
                throw new ConflictException(
                    $"Phiên đóng kho hiện tại đang ở trạng thái '{latestClosure.Status}'. Không thể tiếp tục xác nhận đóng kho.");
            }

            if (latestClosure.ResolutionType != null)
            {
                throw new ConflictException(
                    "Phiên đóng kho hiện tại đã được chọn hình thức xử lý. Vui lòng hoàn tất hoặc hủy phiên hiện tại trước khi xác nhận lại.");
            }

            var currentConsumable = inventoryItems
                .Where(i => i.ItemType == "Consumable")
                .Sum(i => i.Quantity);
            var currentReusable = inventoryItems
                .Where(i => i.ItemType == "Reusable")
                .Sum(i => i.Quantity);

            return new InitiateDepotClosureResponse
            {
                DepotId = depot.Id,
                DepotName = depot.Name,
                ClosureId = latestClosure.Id,
                Success = false,
                Message = $"Kho vẫn còn hàng tồn ({currentConsumable} đơn vị tiêu hao, {currentReusable} thiết bị tái sử dụng). " +
                          "Hãy tiếp tục xử lý tồn kho bằng luồng chuyển kho hoặc xử lý bên ngoài trước khi xác nhận đóng kho.",
                RemainingItems = inventoryItems
            };
        }

        if (latestClosure.Status == DepotClosureStatus.Processing)
        {
            throw new ConflictException(
                "Phiên đóng kho hiện tại đang được xử lý bởi tiến trình khác. Vui lòng thử lại sau.");
        }

        if (latestClosure.Status == DepotClosureStatus.TransferPending)
        {
            throw new ConflictException(
                "Kho đang có phiên chuyển kho chưa hoàn tất. Vui lòng hoàn tất hoặc hủy phiên hiện tại trước khi xác nhận đóng kho.");
        }

        if (latestClosure.Status is not (DepotClosureStatus.InProgress or DepotClosureStatus.Completed))
        {
            throw new ConflictException(
                $"Phiên đóng kho hiện tại đang ở trạng thái '{latestClosure.Status}'. Không thể xác nhận đóng kho.");
        }

        DepotClosureRecord closureRecord = latestClosure;
        var isFinalizingResolvedClosure = latestClosure.Status == DepotClosureStatus.Completed;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            depot.CompleteClosing();
            await depotRepository.UpdateAsync(depot, cancellationToken);

            if (!isFinalizingResolvedClosure)
            {
                closureRecord.Complete(DateTime.UtcNow);
                await closureRepository.UpdateAsync(closureRecord, cancellationToken);
            }

            await depotFundDrainService.DrainAllToSystemFundAsync(
                request.DepotId,
                closureRecord.Id,
                request.InitiatedBy,
                cancellationToken);

            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "Depot closed successfully | DepotId={DepotId} ClosureId={ClosureId} FinalizedResolvedClosure={FinalizedResolvedClosure}",
            request.DepotId,
            closureRecord.Id,
            isFinalizingResolvedClosure);

        return new InitiateDepotClosureResponse
        {
            DepotId = depot.Id,
            DepotName = depot.Name,
            ClosureId = closureRecord.Id,
            Success = true,
            Message = isFinalizingResolvedClosure
                ? "Đã xác nhận hoàn tất đóng kho sau khi xử lý xong hàng tồn. Quỹ kho đã được chuyển về quỹ hệ thống và manager đã được gỡ."
                : "Kho không còn hàng tồn nên đã được đóng ngay. Quỹ kho đã được chuyển về quỹ hệ thống và manager đã được gỡ."
        };
    }

    private static string BuildClosingBlockersMessage(RESQ.Domain.Entities.Logistics.Models.DepotClosingBlockersModel blockers)
    {
        var messages = new List<string>();
        if (blockers.HasBlockingReservedConsumables)
        {
            messages.Add(
                $"{blockers.ReservedConsumableItemCount} dòng hàng tiêu hao đang có reserved quantity " +
                $"(tổng {blockers.ReservedConsumableUnitCount} đơn vị)");
        }

        if (blockers.HasBlockingReusableStates)
        {
            messages.Add(
                $"{blockers.NonAvailableReusableItemModelCount} loại vật phẩm tái sử dụng còn ở trạng thái khác Available " +
                $"(tổng {blockers.NonAvailableReusableUnitCount} đơn vị)");
        }

        return $"Không thể xác nhận đóng kho vì kho vẫn còn {string.Join(" và ", messages)}. " +
               "Hãy xử lý hết reserved quantity và đưa toàn bộ đồ tái sử dụng về trạng thái Available trước.";
    }
}
