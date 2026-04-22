using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.StartDepotClosing;

public class StartDepotClosingCommandHandler(
    IManagerDepotAccessService managerDepotAccessService,
    IDepotRepository depotRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotClosureRepository depotClosureRepository,
    IDepotClosureTransferRepository depotClosureTransferRepository,
    IUserPermissionResolver permissionResolver,
    IOperationalHubService operationalHubService,
    IUnitOfWork unitOfWork,
    ILogger<StartDepotClosingCommandHandler> logger)
    : IRequestHandler<StartDepotClosingCommand, StartDepotClosingResponse>
{
    public async Task<StartDepotClosingResponse> Handle(
        StartDepotClosingCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "StartDepotClosing | DepotId={DepotId} RequestedBy={RequestedBy}",
            request.DepotId,
            request.RequestedBy);

        var userPermissions = await permissionResolver.GetEffectivePermissionCodesAsync(
            request.RequestedBy,
            cancellationToken);

        var isAdmin = userPermissions.Contains(
            PermissionConstants.InventoryGlobalManage,
            StringComparer.OrdinalIgnoreCase);

        if (!isAdmin)
        {
            var managedDepotId = await managerDepotAccessService.ResolveAccessibleDepotIdAsync(
                request.RequestedBy,
                request.DepotId,
                cancellationToken);

            if (!managedDepotId.HasValue)
            {
                throw ExceptionCodes.WithCode(
                    new ForbiddenException("Tài khoản quản lý kho chưa được gán kho phụ trách."),
                    LogisticsErrorCodes.DepotManagerNotAssigned);
            }

            if (managedDepotId.Value != request.DepotId)
                throw new ForbiddenException("Bạn chỉ có thể thao tác đóng kho mình đang quản lý.");
        }

        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        var activeClosure = await depotClosureRepository.GetActiveClosureByDepotIdAsync(
            request.DepotId,
            cancellationToken);

        if (depot.Status == DepotStatus.Closing)
        {
            if (activeClosure == null)
            {
                throw new ConflictException(
                    "Kho đang ở trạng thái Closing nhưng chưa có phiên đóng kho hợp lệ. Vui lòng kiểm tra lại dữ liệu kho.");
            }

            return new StartDepotClosingResponse
            {
                DepotId = depot.Id,
                ClosureId = activeClosure.Id,
                Status = depot.Status.ToString(),
                Message = "Kho đã ở trạng thái Closing. Bạn có thể tiếp tục bước xác nhận đóng kho."
            };
        }

        if (depot.Status == DepotStatus.Closed)
            throw new ConflictException("Kho đã đóng vĩnh viễn, không thể mở lại quy trình đóng kho.");

        if (activeClosure?.Status == DepotClosureStatus.Processing)
        {
            throw new ConflictException(
                "Phiên đóng kho hiện tại đang được xử lý bởi tiến trình khác. Vui lòng thử lại sau.");
        }

        if (activeClosure is { Status: DepotClosureStatus.TransferPending } || activeClosure?.ResolutionType != null)
        {
            throw new ConflictException(
                "Kho đang có một phiên đóng kho đang xử lý dở. Vui lòng hoàn tất hoặc hủy phiên hiện tại trước khi thao tác lại.");
        }

        var activeCount = await depotRepository.GetActiveDepotCountExcludingAsync(request.DepotId, cancellationToken);
        if (activeCount == 0)
        {
            throw new ConflictException("Không thể đóng kho duy nhất còn đang hoạt động trong hệ thống.");
        }

        var (asSource, asRequester) = await depotRepository.GetNonTerminalSupplyRequestCountsAsync(
            request.DepotId,
            cancellationToken);

        if (asSource + asRequester > 0)
        {
            throw new ConflictException(
                $"Kho hiện có {asSource + asRequester} đơn tiếp tế chưa hoàn tất " +
                $"({asSource} là kho nguồn, {asRequester} là kho yêu cầu). " +
                "Hãy hoàn thành hoặc hủy tất cả đơn tiếp tế trước khi chuyển sang trạng thái Closing.");
        }

        var hasMissionCommitments = await depotInventoryRepository.HasActiveInventoryCommitmentsAsync(
            request.DepotId,
            cancellationToken);

        if (hasMissionCommitments)
        {
            throw new ConflictException(
                "Kho đang có vật phẩm được đặt trước hoặc đang sử dụng trong nhiệm vụ cứu hộ đang diễn ra. " +
                "Hãy hoàn thành hoặc hủy các tương tác nhiệm vụ trước khi chuyển kho sang Closing.");
        }

        var closingBlockers = await depotInventoryRepository.GetDepotClosingBlockersAsync(
            request.DepotId,
            cancellationToken);
        if (closingBlockers.HasAnyBlockingItems)
        {
            throw new ConflictException(BuildClosingBlockersMessage(closingBlockers, "Closing"));
        }

        var relatedTransfers = await depotClosureTransferRepository.GetByRelatedDepotIdAsync(
            request.DepotId,
            cancellationToken);

        var openTransfer = relatedTransfers.FirstOrDefault(t =>
            !string.Equals(t.Status, "Received", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(t.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));

        if (openTransfer != null)
        {
            throw new ConflictException(
                "Kho đang tham gia một phiên chuyển hàng khi đóng kho chưa hoàn tất. " +
                "Vui lòng hoàn tất hoặc hủy phiên chuyển hàng hiện tại trước khi chuyển sang Closing.");
        }

        var currentStatus = depot.Status;
        var inventorySnapshot = await depotRepository.GetDetailedInventoryForClosureAsync(
            request.DepotId,
            cancellationToken);

        DepotClosureRecord closureRecord = activeClosure ?? DepotClosureRecord.Create(
            depotId: request.DepotId,
            initiatedBy: request.RequestedBy,
            closeReason: "Bắt đầu quy trình đóng kho",
            previousStatus: currentStatus,
            snapshotConsumableUnits: inventorySnapshot
                .Where(i => i.ItemType == "Consumable")
                .Sum(i => i.Quantity),
            snapshotReusableUnits: inventorySnapshot
                .Where(i => i.ItemType == "Reusable")
                .Sum(i => i.Quantity),
            totalConsumableRows: inventorySnapshot.Count(i => i.ItemType == "Consumable"),
            totalReusableUnits: inventorySnapshot
                .Where(i => i.ItemType == "Reusable")
                .Sum(i => i.Quantity));

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            depot.ChangeStatus(DepotStatus.Closing, request.RequestedBy);
            await depotRepository.UpdateAsync(depot, cancellationToken);

            if (closureRecord.Id == 0)
            {
                var closureId = await depotClosureRepository.CreateAsync(closureRecord, cancellationToken);
                closureRecord.SetGeneratedId(closureId);
            }

            await unitOfWork.SaveAsync();
        });

        await Task.WhenAll(
            operationalHubService.PushDepotClosureUpdateAsync(
                new DepotClosureRealtimeUpdate
                {
                    SourceDepotId = depot.Id,
                    ClosureId = closureRecord.Id,
                    EntityType = "Closure",
                    Action = "StartedClosing",
                    Status = closureRecord.Status.ToString()
                },
                cancellationToken),
            operationalHubService.PushDepotInventoryUpdateAsync(depot.Id, "StatusChange", cancellationToken),
            operationalHubService.PushLogisticsUpdateAsync("depots", cancellationToken: cancellationToken));

        return new StartDepotClosingResponse
        {
            DepotId = depot.Id,
            ClosureId = closureRecord.Id,
            Status = depot.Status.ToString(),
            Message = "Kho đã được chuyển sang trạng thái Closing. Hệ thống sẵn sàng cho bước xác nhận đóng kho."
        };
    }

    private static string BuildClosingBlockersMessage(RESQ.Domain.Entities.Logistics.Models.DepotClosingBlockersModel blockers, string targetStatus)
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

        return $"Không thể chuyển kho sang {targetStatus} vì kho vẫn còn {string.Join(" và ", messages)}. " +
               "Hãy xử lý hết reserved quantity và đưa toàn bộ đồ tái sử dụng về trạng thái Available trước.";
    }
}
