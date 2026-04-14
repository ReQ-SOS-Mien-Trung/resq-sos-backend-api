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
/// Admin phân b? hàng t?n sang m?t ho?c nhi?u kho dích d? hoàn t?t dóng kho ngu?n.
/// T? d?ng t?o m?t DepotClosureRecord và nhi?u DepotClosureTransferRecord tuong ?ng.
/// </summary>
public class InitiateDepotClosureTransferCommandHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotClosureTransferRepository transferRepository,
    IDepotInventoryRepository inventoryRepository,
    IFirebaseService firebaseService,
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
            ?? throw new NotFoundException("Không tìm th?y kho ngu?n.");

        if (depot.Status != DepotStatus.Unavailable)
        {
            throw new ConflictException(
                $"Kho dang ? tr?ng thái '{depot.Status}'. Ph?i chuy?n sang Unavailable tru?c khi chuy?n hàng.");
        }

        var activeCount = await depotRepository.GetActiveDepotCountExcludingAsync(request.DepotId, cancellationToken);
        if (activeCount == 0)
        {
            throw new ConflictException("Không th? dóng kho duy nh?t còn dang ho?t d?ng trong h? th?ng.");
        }

        var existingClosure = await closureRepository.GetActiveClosureByDepotIdAsync(request.DepotId, cancellationToken);
        if (existingClosure != null)
        {
            throw new ConflictException("Kho dang có phiên chuy?n kho chua hoàn t?t. H?y phiên cu tru?c khi t?o m?i.");
        }

        var remainingItems = await depotRepository.GetDetailedInventoryForClosureAsync(request.DepotId, cancellationToken);
        var inventoryLookup = remainingItems.ToDictionary(
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

        var targetDepotIds = normalizedAssignments
            .Select(x => x.TargetDepotId)
            .Distinct()
            .ToList();

        var targetDepots = new Dictionary<int, DepotModel>();
        foreach (var targetDepotId in targetDepotIds)
        {
            var targetDepot = await depotRepository.GetByIdAsync(targetDepotId, cancellationToken)
                ?? throw new NotFoundException($"Không tìm th?y kho dích #{targetDepotId}.");

            if (targetDepot.Status is DepotStatus.Unavailable or DepotStatus.Closed)
            {
                throw new ConflictException(
                    $"Kho dích '{targetDepot.Name}' không kh? d?ng (tr?ng thái: {targetDepot.Status}). Vui lòng ch?n kho khác.");
            }

            targetDepots[targetDepotId] = targetDepot;
        }

        foreach (var targetGroup in normalizedAssignments.GroupBy(x => x.TargetDepotId))
        {
            var targetDepot = targetDepots[targetGroup.Key];
            var requiredVolume = targetGroup
                .Where(x => string.Equals(x.ItemType, "Consumable", StringComparison.OrdinalIgnoreCase))
                .Sum(x =>
                {
                    var item = inventoryLookup[BuildItemKey(x.ItemModelId, x.ItemType)];
                    return (item.VolumePerUnit ?? 0m) * x.Quantity;
                });

            var availableVolumeCapacity = targetDepot.Capacity - targetDepot.CurrentUtilization;
            if (requiredVolume > availableVolumeCapacity)
            {
                throw new ConflictException(
                    $"Kho dích '{targetDepot.Name}' không d? s?c ch?a th? tích cho ph?n hàng du?c phân b?. " +
                    $"C?n: {requiredVolume:N0} — Còn tr?ng: {availableVolumeCapacity:N0} dm³.");
            }

            var requiredWeight = targetGroup
                .Where(x => string.Equals(x.ItemType, "Consumable", StringComparison.OrdinalIgnoreCase))
                .Sum(x =>
                {
                    var item = inventoryLookup[BuildItemKey(x.ItemModelId, x.ItemType)];
                    return (item.WeightPerUnit ?? 0m) * x.Quantity;
                });

            var availableWeightCapacity = targetDepot.WeightCapacity - targetDepot.CurrentWeightUtilization;
            if (requiredWeight > availableWeightCapacity)
            {
                throw new ConflictException(
                    $"Kho dích '{targetDepot.Name}' không d? s?c ch?a cân n?ng cho ph?n hàng du?c phân b?. " +
                    $"C?n: {requiredWeight:N0} — Còn tr?ng: {availableWeightCapacity:N0} kg.");
            }
        }

        var consumableVolume = await depotRepository.GetConsumableTransferVolumeAsync(request.DepotId, cancellationToken);
        var consumableRowCount = await depotRepository.GetConsumableInventoryRowCountAsync(request.DepotId, cancellationToken);
        var (reusableAvailable, reusableInUse) = await depotRepository.GetReusableItemCountsAsync(request.DepotId, cancellationToken);

        var closure = DepotClosureRecord.Create(
            depotId: request.DepotId,
            initiatedBy: request.InitiatedBy,
            closeReason: request.Reason,
            previousStatus: depot.Status,
            snapshotConsumableUnits: (int)consumableVolume,
            snapshotReusableUnits: reusableAvailable + reusableInUse,
            totalConsumableRows: consumableRowCount,
            totalReusableUnits: reusableAvailable + reusableInUse);
        closure.SetTransferResolution(targetDepotIds.Count == 1 ? targetDepotIds[0] : null);

        var transferSummaries = new List<InitiateDepotClosureTransferSummaryDto>();

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var closureId = await closureRepository.CreateAsync(closure, cancellationToken);
            closure.SetGeneratedId(closureId);

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
                        "Kho c?a b?n s?p ti?p nh?n hàng chuy?n kho",
                        $"Admin dã ch? d?nh '{transferSummary.TargetDepotName}' ti?p nh?n m?t ph?n hàng t? kho '{depot.Name}' dang dóng c?a.",
                        "depot_closure_transfer_assigned",
                        new Dictionary<string, string>
                        {
                            ["sourceDepotId"] = request.DepotId.ToString(),
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

        return new InitiateDepotClosureTransferResponse
        {
            ClosureId = closure.Id,
            SourceDepotId = request.DepotId,
            SourceDepotName = depot.Name,
            Transfers = transferSummaries.OrderBy(x => x.TargetDepotName).ThenBy(x => x.TransferId).ToList(),
            ReusableItemsSkipped = reusableInUse,
            Message = transferSummaries.Count == 1
                ? $"Ðã t?o k? ho?ch chuy?n hàng sang kho '{transferSummaries[0].TargetDepotName}'. Manager kho ngu?n và kho dích ti?p t?c xác nh?n theo t?ng bu?c."
                : $"Ðã t?o k? ho?ch phân b? hàng t?n sang {transferSummaries.Count} kho dích. M?i kho s? nh?n m?t transfer riêng d? xác nh?n."
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
                    $"v?t ph?m #{assignment.ItemModelId} ({assignment.ItemType}) không t?n t?i trong t?n kho c?a kho ngu?n.");
            }

            if (assignment.TargetDepotId == sourceDepotId)
            {
                throw new ConflictException($"v?t ph?m '{item.ItemName}' không du?c phân b? v? chính kho ngu?n.");
            }

            if (assignment.Quantity > item.TransferableQuantity)
            {
                throw new ConflictException(
                    $"v?t ph?m '{item.ItemName}' ch? có th? chuy?n {item.TransferableQuantity} don v? nhung yêu c?u phân b? {assignment.Quantity}.");
            }
        }

        foreach (var item in inventoryLookup.Values.Where(x => x.TransferableQuantity > 0))
        {
            var assignedQuantity = assignments
                .Where(x => x.ItemModelId == item.ItemModelId &&
                            string.Equals(x.ItemType, item.ItemType, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Quantity);

            if (assignedQuantity != item.TransferableQuantity)
            {
                throw new ConflictException(
                    $"v?t ph?m '{item.ItemName}' c?n du?c phân b? d? {item.TransferableQuantity} don v? có th? chuy?n. Hi?n m?i phân b? {assignedQuantity}.");
            }
        }
    }

    private static string BuildItemKey(int itemModelId, string itemType)
        => $"{itemModelId}:{itemType.Trim().ToUpperInvariant()}";

    private static string BuildAssignmentKey(int targetDepotId, int itemModelId, string itemType)
        => $"{targetDepotId}:{BuildItemKey(itemModelId, itemType)}";

    private sealed record NormalizedAssignment(
        int TargetDepotId,
        int ItemModelId,
        string ItemType,
        int Quantity);
}
