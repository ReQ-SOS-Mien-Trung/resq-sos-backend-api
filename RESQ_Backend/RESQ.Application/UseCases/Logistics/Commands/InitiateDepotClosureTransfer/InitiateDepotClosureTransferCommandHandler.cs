using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosureTransfer;

/// <summary>
/// Admin phân bổ hàng tồn sang một hoặc nhiều kho đích để hoàn tất đóng kho nguồn.
/// Tự động tạo một DepotClosureRecord và nhiều DepotClosureTransferRecord tương ứng.
/// </summary>
public class InitiateDepotClosureTransferCommandHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotClosureTransferRepository transferRepository,
    IDepotInventoryRepository inventoryRepository,
    IFirebaseService firebaseService,
    IOperationalHubService operationalHubService,
    IUnitOfWork unitOfWork,
    ILogger<InitiateDepotClosureTransferCommandHandler> logger)
    : IRequestHandler<InitiateDepotClosureTransferCommand, InitiateDepotClosureTransferResponse>
{
    public async Task<InitiateDepotClosureTransferResponse> Handle(
        InitiateDepotClosureTransferCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "InitiateDepotClosureTransfer | SourceDepot={Src} Targets={Targets} By={By}",
            request.DepotId,
            string.Join(",", request.Assignments.Select(x => x.TargetDepotId).Distinct()),
            request.InitiatedBy);

        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho nguồn.");

        if (depot.Status != DepotStatus.Closing)
        {
            throw new ConflictException(
                $"Kho đang ở trạng thái '{depot.Status}'. Phải chuyển sang Closing trước khi chuyển hàng.");
        }

        var activeCount = await depotRepository.GetActiveDepotCountExcludingAsync(request.DepotId, cancellationToken);
        if (activeCount == 0)
        {
            throw new ConflictException("Không thể đóng kho duy nhất còn đang hoạt động trong hệ thống.");
        }

        var existingClosure = await closureRepository.GetActiveClosureByDepotIdAsync(request.DepotId, cancellationToken)
            ?? throw new ConflictException(
                "Không tìm thấy phiên đóng kho đang mở cho kho này. Vui lòng gọi POST /{id}/closed trước để hệ thống kiểm tra và khởi tạo phiên đóng kho.");

        if (existingClosure.Status == DepotClosureStatus.Processing)
        {
            throw new ConflictException("Phiên đóng kho hiện tại đang được xử lý bởi tiến trình khác. Vui lòng thử lại sau.");
        }

        if (existingClosure.Status == DepotClosureStatus.TransferPending)
        {
            var hasOpenTransfers = await transferRepository.HasOpenTransfersAsync(existingClosure.Id, cancellationToken);
            if (!hasOpenTransfers)
            {
                var remainingItems = await depotRepository.GetDetailedInventoryForClosureAsync(request.DepotId, cancellationToken);
                if (remainingItems.Count > 0)
                {
                    existingClosure.ReopenForResidualHandling();
                }
                else
                {
                    existingClosure.Complete(existingClosure.CompletedAt ?? DateTime.UtcNow);
                }

                await closureRepository.UpdateAsync(existingClosure, cancellationToken);
                await unitOfWork.SaveAsync();
            }
        }

        if (existingClosure.Status == DepotClosureStatus.TransferPending)
        {
            throw new ConflictException("Kho đang có phiên chuyển kho chưa hoàn tất. Hủy hoặc hoàn tất phiên cũ trước khi tạo mới.");
        }

        if (existingClosure.ResolutionType != null)
        {
            throw new ConflictException(
                "Phiên đóng kho hiện tại đã được chọn hình thức xử lý. Vui lòng hoàn tất hoặc hủy phiên hiện tại trước khi thao tác lại.");
        }

        var currentInventoryItems = await depotRepository.GetDetailedInventoryForClosureAsync(request.DepotId, cancellationToken);
        var inventoryLookup = currentInventoryItems.ToDictionary(
            item => BuildItemKey(item.ItemModelId, item.ItemType),
            item => item);

        var normalizedAssignments = request.Assignments
            .SelectMany(targetAssignment => targetAssignment.Items.Select(item => new NormalizedAssignment(
                targetAssignment.TargetDepotId,
                item.ItemModelId,
                item.ItemType.Trim(),
                item.Quantity)))
            .GroupBy(x => BuildAssignmentKey(x.TargetDepotId, x.ItemModelId, x.ItemType))
            .Select(g => new NormalizedAssignment(
                g.First().TargetDepotId,
                g.First().ItemModelId,
                g.First().ItemType,
                g.Sum(x => x.Quantity)))
            .ToList();

        ValidateAssignments(request.DepotId, normalizedAssignments, inventoryLookup);

        var remainingAssignmentItems = inventoryLookup.Values
            .Select(item =>
            {
                var assignedQuantity = normalizedAssignments
                    .Where(x => x.ItemModelId == item.ItemModelId &&
                                string.Equals(x.ItemType, item.ItemType, StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Quantity);

                var remainingTransferableQuantity = Math.Max(0, item.TransferableQuantity - assignedQuantity);
                return new InitiateDepotClosureTransferRemainingItemDto
                {
                    ItemModelId = item.ItemModelId,
                    ItemName = item.ItemName,
                    CategoryName = item.CategoryName,
                    ItemType = item.ItemType,
                    Unit = item.Unit,
                    CurrentQuantity = item.Quantity,
                    AssignedQuantity = assignedQuantity,
                    RemainingTransferableQuantity = remainingTransferableQuantity,
                    BlockedQuantity = item.BlockedQuantity
                };
            })
            .Where(x => x.RemainingTransferableQuantity > 0 || x.BlockedQuantity > 0)
            .OrderBy(x => x.ItemType)
            .ThenBy(x => x.ItemName)
            .ToList();

        var targetDepotIds = normalizedAssignments
            .Select(x => x.TargetDepotId)
            .Distinct()
            .ToList();

        var targetDepots = new Dictionary<int, DepotModel>();
        foreach (var targetDepotId in targetDepotIds)
        {
            var targetDepot = await depotRepository.GetByIdAsync(targetDepotId, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy kho đích #{targetDepotId}.");

            if (targetDepot.Status is DepotStatus.Unavailable or DepotStatus.Closing or DepotStatus.Closed)
            {
                throw new ConflictException(
                    $"Kho đích '{targetDepot.Name}' không khả dụng (trạng thái: {targetDepot.Status}). Vui lòng chọn kho khác.");
            }

            targetDepots[targetDepotId] = targetDepot;
        }

        foreach (var targetGroup in normalizedAssignments.GroupBy(x => x.TargetDepotId))
        {
            var targetDepot = targetDepots[targetGroup.Key];
            var requiredVolume = targetGroup
                .Sum(x =>
                {
                    var item = inventoryLookup[BuildItemKey(x.ItemModelId, x.ItemType)];
                    return ResolveVolumePerUnit(item) * x.Quantity;
                });

            var availableVolumeCapacity = targetDepot.Capacity - targetDepot.CurrentUtilization;
            if (requiredVolume > availableVolumeCapacity)
            {
                throw new ConflictException(
                    $"Kho đích '{targetDepot.Name}' không đủ sức chứa thể tích cho phần hàng được phân bổ. " +
                    $"Cần: {requiredVolume:N0} — Còn trống: {availableVolumeCapacity:N0} dm³.");
            }

            var requiredWeight = targetGroup
                .Sum(x =>
                {
                    var item = inventoryLookup[BuildItemKey(x.ItemModelId, x.ItemType)];
                    return ResolveWeightPerUnit(item) * x.Quantity;
                });

            var availableWeightCapacity = targetDepot.WeightCapacity - targetDepot.CurrentWeightUtilization;
            if (requiredWeight > availableWeightCapacity)
            {
                throw new ConflictException(
                    $"Kho đích '{targetDepot.Name}' không đủ sức chứa cân nặng cho phần hàng được phân bổ. " +
                    $"Cần: {requiredWeight:N0} — Còn trống: {availableWeightCapacity:N0} kg.");
            }
        }

        var consumableVolume = await depotRepository.GetConsumableTransferVolumeAsync(request.DepotId, cancellationToken);
        var consumableRowCount = await depotRepository.GetConsumableInventoryRowCountAsync(request.DepotId, cancellationToken);
        var (reusableAvailable, reusableInUse) = await depotRepository.GetReusableItemCountsAsync(request.DepotId, cancellationToken);

        var closure = existingClosure;
        closure.SetTransferResolution(targetDepotIds.Count == 1 ? targetDepotIds[0] : null);

        var transferSummaries = new List<InitiateDepotClosureTransferSummaryDto>();

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var closureId = closure.Id;

            foreach (var targetGroup in normalizedAssignments.GroupBy(x => x.TargetDepotId))
            {
                var targetDepot = targetDepots[targetGroup.Key];
                var transferItems = targetGroup
                    .Select(x =>
                    {
                        var item = inventoryLookup[BuildItemKey(x.ItemModelId, x.ItemType)];
                        return new
                        {
                            Assignment = x,
                            Item = item,
                            Record = DepotClosureTransferItemRecord.Create(
                                x.ItemModelId,
                                item.ItemName,
                                item.ItemType,
                                item.Unit,
                                x.Quantity)
                        };
                    })
                    .ToList();

                var transfer = DepotClosureTransferRecord.Create(
                    closureId: closureId,
                    sourceDepotId: request.DepotId,
                    targetDepotId: targetGroup.Key,
                    snapshotConsumableUnits: transferItems
                        .Where(x => string.Equals(x.Assignment.ItemType, "Consumable", StringComparison.OrdinalIgnoreCase))
                        .Sum(x => x.Assignment.Quantity),
                    snapshotReusableUnits: transferItems
                        .Where(x => string.Equals(x.Assignment.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase))
                        .Sum(x => x.Assignment.Quantity));

                var transferId = await transferRepository.CreateAsync(
                    transfer,
                    transferItems.Select(x => x.Record).ToList(),
                    cancellationToken);

                await inventoryRepository.ReserveForClosurePreparationAsync(
                    request.DepotId,
                    transferId,
                    closureId,
                    request.InitiatedBy,
                    transferItems.Select(x => new DepotClosureTransferItemMoveDto
                    {
                        ItemModelId = x.Record.ItemModelId,
                        ItemType = x.Record.ItemType,
                        Quantity = x.Record.Quantity
                    }).ToList(),
                    cancellationToken);

                transferSummaries.Add(new InitiateDepotClosureTransferSummaryDto
                {
                    TransferId = transferId,
                    TargetDepotId = targetDepot.Id,
                    TargetDepotName = targetDepot.Name,
                    TransferStatus = transfer.Status,
                    SnapshotConsumableUnits = transfer.SnapshotConsumableUnits,
                    SnapshotReusableUnits = transfer.SnapshotReusableUnits,
                    Items = transferItems.Select(x => new InitiateDepotClosureTransferItemDto
                    {
                        ItemModelId = x.Record.ItemModelId,
                        ItemName = x.Record.ItemName,
                        ItemType = x.Record.ItemType,
                        Unit = x.Record.Unit,
                        Quantity = x.Record.Quantity
                    }).ToList()
                });
            }

            closure.MarkTransferPending();
            await closureRepository.UpdateAsync(closure, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        var realtimeTasks = new List<Task>
        {
            operationalHubService.PushDepotInventoryUpdateAsync(
                request.DepotId,
                "ClosureTransferReserved",
                cancellationToken),
            operationalHubService.PushDepotClosureUpdateAsync(
                new RESQ.Application.Common.Models.DepotClosureRealtimeUpdate
                {
                    SourceDepotId = request.DepotId,
                    TargetDepotId = targetDepotIds.Count == 1 ? targetDepotIds[0] : null,
                    ClosureId = closure.Id,
                    EntityType = "Closure",
                    Action = "TransferPending",
                    Status = closure.Status.ToString()
                },
                cancellationToken)
        };

        realtimeTasks.AddRange(transferSummaries.Select(transferSummary =>
            operationalHubService.PushDepotClosureUpdateAsync(
                new RESQ.Application.Common.Models.DepotClosureRealtimeUpdate
                {
                    SourceDepotId = request.DepotId,
                    TargetDepotId = transferSummary.TargetDepotId,
                    ClosureId = closure.Id,
                    TransferId = transferSummary.TransferId,
                    EntityType = "Transfer",
                    Action = "AwaitingPreparation",
                    Status = transferSummary.TransferStatus
                },
                cancellationToken)));

        await Task.WhenAll(realtimeTasks);

        foreach (var transferSummary in transferSummaries)
        {
            try
            {
                var targetManagerId = await inventoryRepository.GetActiveManagerUserIdByDepotIdAsync(
                    transferSummary.TargetDepotId, cancellationToken);

                if (targetManagerId.HasValue)
                {
                    await firebaseService.SendNotificationToUserAsync(
                        targetManagerId.Value,
                        "Kho của bạn sắp tiếp nhận hàng chuyển kho",
                        $"Admin đã chỉ định '{transferSummary.TargetDepotName}' tiếp nhận một phần hàng từ kho '{depot.Name}' đang đóng cửa.",
                        "depot_closure_transfer_assigned",
                        new Dictionary<string, string>
                        {
                            ["closureId"] = closure.Id.ToString(),
                            ["sourceDepotId"] = request.DepotId.ToString(),
                            ["targetDepotId"] = transferSummary.TargetDepotId.ToString(),
                            ["transferId"] = transferSummary.TransferId.ToString()
                        },
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify target manager | TransferId={Id}", transferSummary.TransferId);
            }
        }

        try
        {
            var sourceManagerId = await inventoryRepository.GetActiveManagerUserIdByDepotIdAsync(
                request.DepotId,
                cancellationToken);

            if (sourceManagerId.HasValue)
            {
                await firebaseService.SendNotificationToUserAsync(
                    sourceManagerId.Value,
                    "Admin đã tạo phương án chuyển kho để đóng kho",
                    transferSummaries.Count == 1
                        ? $"Admin đã lập 1 đợt chuyển hàng từ kho '{depot.Name}' sang kho '{transferSummaries[0].TargetDepotName}'."
                        : $"Admin đã lập {transferSummaries.Count} đợt chuyển hàng từ kho '{depot.Name}' sang các kho đích để xử lý đóng kho.",
                    "depot_closure_transfer_assigned",
                    new Dictionary<string, string>
                    {
                        ["closureId"] = closure.Id.ToString(),
                        ["sourceDepotId"] = request.DepotId.ToString(),
                        ["transferCount"] = transferSummaries.Count.ToString()
                    },
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify source manager for closure transfer plan | ClosureId={ClosureId}", closure.Id);
        }

        return new InitiateDepotClosureTransferResponse
        {
            ClosureId = closure.Id,
            SourceDepotId = request.DepotId,
            SourceDepotName = depot.Name,
            Transfers = transferSummaries.OrderBy(x => x.TargetDepotName).ThenBy(x => x.TransferId).ToList(),
            ReusableItemsSkipped = reusableInUse,
            HasRemainingItems = remainingAssignmentItems.Count > 0,
            RemainingItems = remainingAssignmentItems,
            Message = BuildTransferPlanMessage(transferSummaries, remainingAssignmentItems.Count > 0)
        };
    }

    private static void ValidateAssignments(
        int sourceDepotId,
        IReadOnlyCollection<NormalizedAssignment> assignments,
        IReadOnlyDictionary<string, ClosureInventoryItemDto> inventoryLookup)
    {
        foreach (var assignment in assignments)
        {
            var key = BuildItemKey(assignment.ItemModelId, assignment.ItemType);
            if (!inventoryLookup.TryGetValue(key, out var item))
            {
                throw new ConflictException(
                    $"vật phẩm #{assignment.ItemModelId} ({assignment.ItemType}) không tồn tại trong tồn kho của kho nguồn.");
            }

            if (assignment.TargetDepotId == sourceDepotId)
            {
                throw new ConflictException($"vật phẩm '{item.ItemName}' không được phân bổ về chính kho nguồn.");
            }

            if (assignment.Quantity > item.TransferableQuantity)
            {
                throw new ConflictException(
                    $"vật phẩm '{item.ItemName}' chỉ có thể chuyển {item.TransferableQuantity} đơn vị nhưng yêu cầu phân bổ {assignment.Quantity}.");
            }
        }

        foreach (var item in inventoryLookup.Values.Where(x => x.TransferableQuantity > 0))
        {
            var assignedQuantity = assignments
                .Where(x => x.ItemModelId == item.ItemModelId &&
                            string.Equals(x.ItemType, item.ItemType, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Quantity);

            if (assignedQuantity > item.TransferableQuantity)
            {
                throw new ConflictException(
                    $"vật phẩm '{item.ItemName}' chỉ có thể phân bổ tối đa {item.TransferableQuantity} đơn vị có thể chuyển. Hiện yêu cầu {assignedQuantity}.");
            }
        }
    }

    private static string BuildTransferPlanMessage(
        IReadOnlyCollection<InitiateDepotClosureTransferSummaryDto> transferSummaries,
        bool hasRemainingItems)
    {
        var transferMessage = transferSummaries.Count == 1
            ? $"Đã tạo kế hoạch chuyển hàng sang kho '{transferSummaries.First().TargetDepotName}'."
            : $"Đã tạo kế hoạch phân bổ hàng tồn sang {transferSummaries.Count} kho đích.";

        if (hasRemainingItems)
        {
            return $"{transferMessage} Sau khi các transfer hiện tại hoàn tất hoặc bị hủy, admin có thể chọn tiếp chuyển kho đợt khác hoặc đánh dấu xử lý bên ngoài cho phần còn lại.";
        }

        return $"{transferMessage} Toàn bộ phần hàng có thể chuyển đã được đưa vào transfer batch hiện tại.";
    }

    private static string BuildItemKey(int itemModelId, string itemType)
        => $"{itemModelId}:{itemType.Trim().ToUpperInvariant()}";

    private static string BuildAssignmentKey(int targetDepotId, int itemModelId, string itemType)
        => $"{targetDepotId}:{BuildItemKey(itemModelId, itemType)}";

    private static decimal ResolveVolumePerUnit(ClosureInventoryItemDto item)
        => item.VolumePerUnit.GetValueOrDefault(0.01m);

    private static decimal ResolveWeightPerUnit(ClosureInventoryItemDto item)
        => item.WeightPerUnit.GetValueOrDefault(0.01m);

    private sealed record NormalizedAssignment(
        int TargetDepotId,
        int ItemModelId,
        string ItemType,
        int Quantity);
}
