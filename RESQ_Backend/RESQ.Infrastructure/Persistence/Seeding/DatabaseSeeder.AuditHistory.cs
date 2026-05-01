using Microsoft.EntityFrameworkCore;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed partial class DatabaseSeeder
{
    private async Task SeedAuditAndHistoryAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        await SeedInventoryMovementHistoryAsync(seed, cancellationToken);

        foreach (var depot in seed.Depots)
        {
            _db.InventoryStockThresholdConfigs.Add(new InventoryStockThresholdConfig
            {
                ScopeType = "DEPOT",
                DepotId = depot.Id,
                DangerRatio = 0.18m,
                WarningRatio = 0.35m,
                MinimumThreshold = 120,
                IsActive = true,
                UpdatedBy = seed.Managers[depot.Id % seed.Managers.Count].Id,
                UpdatedAt = seed.AnchorUtc.AddDays(-depot.Id),
                RowVersion = 1
            });
        }

        foreach (var category in seed.Categories.Take(20))
        {
            var depot = seed.Depots[category.Id % seed.Depots.Count];
            _db.InventoryStockThresholdConfigs.Add(new InventoryStockThresholdConfig
            {
                ScopeType = "DEPOT_CATEGORY",
                DepotId = depot.Id,
                CategoryId = category.Id,
                DangerRatio = 0.15m,
                WarningRatio = 0.32m,
                MinimumThreshold = 200,
                IsActive = true,
                UpdatedBy = seed.Managers[category.Id % seed.Managers.Count].Id,
                UpdatedAt = seed.AnchorUtc.AddDays(-category.Id),
                RowVersion = 1
            });
        }

        foreach (var item in seed.ItemModels.Take(30))
        {
            var depot = seed.Depots[item.Id % seed.Depots.Count];
            _db.InventoryStockThresholdConfigs.Add(new InventoryStockThresholdConfig
            {
                ScopeType = "DEPOT_ITEM",
                DepotId = depot.Id,
                ItemModelId = item.Id,
                DangerRatio = 0.12m,
                WarningRatio = 0.30m,
                MinimumThreshold = item.ItemType == "Reusable" ? 3 : 80,
                IsActive = true,
                UpdatedBy = seed.Managers[item.Id % seed.Managers.Count].Id,
                UpdatedAt = seed.AnchorUtc.AddDays(-item.Id % 60),
                RowVersion = 1
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        var configs = await _db.InventoryStockThresholdConfigs
            .Where(c => c.Id != 1)
            .OrderBy(c => c.Id)
            .Take(90)
            .ToListAsync(cancellationToken);
        foreach (var config in configs)
        {
            _db.InventoryStockThresholdConfigHistories.Add(new InventoryStockThresholdConfigHistory
            {
                ConfigId = config.Id,
                ScopeType = config.ScopeType,
                DepotId = config.DepotId,
                CategoryId = config.CategoryId,
                ItemModelId = config.ItemModelId,
                OldDangerRatio = 0.10m,
                NewDangerRatio = config.DangerRatio,
                OldWarningRatio = 0.25m,
                NewWarningRatio = config.WarningRatio,
                ChangedBy = seed.Managers[config.Id % seed.Managers.Count].Id,
                ChangedAt = seed.AnchorUtc.AddDays(-config.Id % 90),
                ChangeReason = "Mùa mưa bão cần mức dự trữ cao hơn",
                Action = "Update"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedInventoryMovementHistoryAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var vatInvoices = await _db.VatInvoices
            .OrderBy(v => v.Id)
            .ToListAsync(cancellationToken);
        var requestItems = await _db.DepotSupplyRequestItems
            .AsNoTracking()
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);
        var missionItems = await _db.MissionItems
            .AsNoTracking()
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        var vatInvoiceIds = vatInvoices.Select(v => v.Id).ToArray();
        var itemModelsById = seed.ItemModels.ToDictionary(i => i.Id);
        var missionsById = seed.Missions.ToDictionary(m => m.Id);
        var lotsByInventoryId = seed.Lots
            .GroupBy(l => l.SupplyInventoryId)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Id).ToList());
        var inventoriesByDepotItem = seed.Inventories
            .Where(i => i.DepotId.HasValue && i.ItemModelId.HasValue)
            .ToDictionary(i => (i.DepotId!.Value, i.ItemModelId!.Value));

        var consumablePlans = seed.Inventories
            .Where(i => i.DepotId.HasValue
                && i.ItemModelId.HasValue
                && itemModelsById.TryGetValue(i.ItemModelId.Value, out var itemModel)
                && string.Equals(itemModel.ItemType, "Consumable", StringComparison.Ordinal)
                && lotsByInventoryId.ContainsKey(i.Id))
            .Select(i =>
            {
                var seedImportLots = lotsByInventoryId[i.Id];
                return new ConsumableInventoryHistoryPlan
                {
                    Inventory = i,
                    ItemModel = itemModelsById[i.ItemModelId!.Value],
                    BaseLot = seedImportLots[0],
                    SupplementalImportLots = seedImportLots.Skip(1).ToList(),
                    PerformedBy = ManagerForDepot(seed, i.DepotId!.Value)
                };
            })
            .ToDictionary(plan => plan.Inventory.Id);

        var transferLogCount = BuildCompletedTransferHistory(
            seed,
            requestItems,
            itemModelsById,
            inventoriesByDepotItem,
            consumablePlans);

        var missionExportTarget = 100 - transferLogCount;
        BuildMissionExportHistory(
            seed,
            missionItems,
            itemModelsById,
            missionsById,
            inventoriesByDepotItem,
            consumablePlans,
            missionExportTarget);

        BuildAdjustmentHistory(consumablePlans.Values.ToList(), seed.AnchorUtc);

        var inventoryLogs = new List<InventoryLog>(820);
        BuildConsumableInventoryHistory(seed, vatInvoiceIds, consumablePlans.Values.ToList(), inventoryLogs);
        BuildReusableInventoryHistory(seed, vatInvoiceIds, inventoryLogs);

        _db.InventoryLogs.AddRange(inventoryLogs);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private int BuildCompletedTransferHistory(
        DemoSeedContext seed,
        IReadOnlyList<DepotSupplyRequestItem> requestItems,
        IReadOnlyDictionary<int, ItemModel> itemModelsById,
        IReadOnlyDictionary<(int DepotId, int ItemModelId), SupplyInventory> inventoriesByDepotItem,
        IReadOnlyDictionary<int, ConsumableInventoryHistoryPlan> consumablePlans)
    {
        var requestItemsByRequestId = requestItems
            .GroupBy(i => i.DepotSupplyRequestId)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Id).ToList());
        var inboundCapacity = consumablePlans.Values.ToDictionary(plan => plan.Inventory.Id, plan => plan.FinalQuantity);
        var transferLogs = 0;

        foreach (var request in seed.SupplyRequests
                     .Where(r => string.Equals(r.SourceStatus, "Completed", StringComparison.Ordinal))
                     .OrderBy(r => r.Id))
        {
            if (transferLogs >= 100 || !requestItemsByRequestId.TryGetValue(request.Id, out var items))
            {
                continue;
            }

            foreach (var item in items)
            {
                if (transferLogs >= 100
                    || !itemModelsById.TryGetValue(item.ItemModelId, out var itemModel)
                    || !string.Equals(itemModel.ItemType, "Consumable", StringComparison.Ordinal)
                    || !inventoriesByDepotItem.TryGetValue((request.SourceDepotId, item.ItemModelId), out var sourceInventory)
                    || !inventoriesByDepotItem.TryGetValue((request.RequestingDepotId, item.ItemModelId), out var destinationInventory)
                    || !consumablePlans.TryGetValue(sourceInventory.Id, out var sourcePlan)
                    || !consumablePlans.TryGetValue(destinationInventory.Id, out var destinationPlan))
                {
                    continue;
                }

                var remainingInboundCapacity = inboundCapacity[destinationPlan.Inventory.Id];
                var quantity = Math.Min(item.Quantity, 10 + item.Id % 18);
                quantity = Math.Min(quantity, Math.Max(0, Math.Min(32, remainingInboundCapacity / 4)));
                if (quantity < 6)
                {
                    continue;
                }

                var shippedAt = ClampHistoricalUtc(
                    request.ShippedAt ?? request.CompletedAt ?? request.CreatedAt.AddHours(3),
                    request.CreatedAt,
                    seed.AnchorUtc);
                var completedAt = ClampHistoricalUtc(
                    request.CompletedAt ?? shippedAt.AddHours(4),
                    shippedAt,
                    seed.AnchorUtc);

                sourcePlan.OutboundEvents.Add(new ConsumableOutboundEvent
                {
                    ActionType = InventoryActionType.TransferOut.ToString(),
                    SourceType = InventorySourceType.Transfer.ToString(),
                    SourceId = request.Id,
                    Quantity = quantity,
                    CreatedAt = shippedAt,
                    PerformedBy = request.ShippedBy ?? request.PreparedBy ?? sourcePlan.PerformedBy,
                    MissionId = null,
                    Note = $"Xuất chuyển {itemModel.Name} từ {request.SourceDepot?.Name ?? $"kho #{request.SourceDepotId}"} sang {request.RequestingDepot?.Name ?? $"kho #{request.RequestingDepotId}"} theo phiếu #{request.Id}"
                });

                destinationPlan.InboundTransfers.Add(new ConsumableInboundTransferEvent
                {
                    Quantity = quantity,
                    SourceId = request.Id,
                    CreatedAt = completedAt,
                    PerformedBy = request.ConfirmedBy ?? request.CompletedBy ?? destinationPlan.PerformedBy,
                    ReceivedDate = completedAt,
                    ExpiredDate = sourcePlan.BaseLot.ExpiredDate,
                    Note = $"Nhận chuyển {itemModel.Name} tại {request.RequestingDepot?.Name ?? $"kho #{request.RequestingDepotId}"} từ phiếu điều phối #{request.Id}"
                });

                inboundCapacity[destinationPlan.Inventory.Id] -= quantity;
                transferLogs += 2;
            }
        }

        return transferLogs;
    }

    private void BuildMissionExportHistory(
        DemoSeedContext seed,
        IReadOnlyList<MissionItem> missionItems,
        IReadOnlyDictionary<int, ItemModel> itemModelsById,
        IReadOnlyDictionary<int, Mission> missionsById,
        IReadOnlyDictionary<(int DepotId, int ItemModelId), SupplyInventory> inventoriesByDepotItem,
        IReadOnlyDictionary<int, ConsumableInventoryHistoryPlan> consumablePlans,
        int missionExportTarget)
    {
        if (missionExportTarget <= 0)
        {
            return;
        }

        var added = 0;
        foreach (var missionItem in missionItems)
        {
            if (added >= missionExportTarget
                || missionItem.SourceDepotId is null
                || missionItem.ItemModelId is null
                || !itemModelsById.TryGetValue(missionItem.ItemModelId.Value, out var itemModel)
                || !string.Equals(itemModel.ItemType, "Consumable", StringComparison.Ordinal)
                || !inventoriesByDepotItem.TryGetValue((missionItem.SourceDepotId.Value, missionItem.ItemModelId.Value), out var inventory)
                || !consumablePlans.TryGetValue(inventory.Id, out var plan)
                || missionItem.MissionId is null
                || !missionsById.TryGetValue(missionItem.MissionId.Value, out var mission)
                || string.Equals(mission.Status, "Planned", StringComparison.Ordinal))
            {
                continue;
            }

            var quantity = missionItem.AllocatedQuantity ?? missionItem.RequiredQuantity ?? 0;
            quantity = Math.Min(quantity, 14 + missionItem.Id % 24);
            if (quantity <= 0)
            {
                continue;
            }

            plan.OutboundEvents.Add(new ConsumableOutboundEvent
            {
                ActionType = InventoryActionType.Export.ToString(),
                SourceType = InventorySourceType.Mission.ToString(),
                SourceId = mission.Id,
                Quantity = quantity,
                CreatedAt = (mission.StartTime ?? mission.CreatedAt ?? seed.StartUtc).AddMinutes(25 + missionItem.Id % 40),
                PerformedBy = plan.PerformedBy,
                MissionId = mission.Id,
                Note = $"Xuất {itemModel.Name} cho mission #{mission.Id} thuộc cụm SOS #{mission.ClusterId}"
            });
            added++;
        }
    }

    private static void BuildAdjustmentHistory(IReadOnlyList<ConsumableInventoryHistoryPlan> plans, DateTime anchorUtc)
    {
        foreach (var plan in plans
                     .OrderBy(p => p.Inventory.Id)
                     .Where(p => p.Inventory.Id % 3 == 0)
                     .Take(45))
        {
            var quantity = Math.Min(3 + plan.Inventory.Id % 8, Math.Max(2, Math.Max(1, plan.FinalQuantity / 30)));
            var baseCreatedAt = plan.Inventory.LastStockedAt ?? plan.BaseLot.ReceivedDate ?? anchorUtc.AddDays(-90);
            var createdAtCandidate = baseCreatedAt.AddDays(18 + plan.Inventory.Id % 40);
            var fallbackCreatedAt = TrimUtcToMinute(anchorUtc.AddHours(-(6 + plan.Inventory.Id % 96)));
            plan.Adjustments.Add(new ConsumableAdjustmentEvent
            {
                Quantity = quantity,
                CreatedAt = createdAtCandidate <= anchorUtc
                    ? createdAtCandidate
                    : ClampHistoricalUtc(fallbackCreatedAt, baseCreatedAt, anchorUtc),
                PerformedBy = plan.PerformedBy,
                Note = $"Điều chỉnh giảm {plan.ItemModel.Name} sau kiểm kê do hư hỏng hoặc quá hạn"
            });
        }
    }

    private void BuildConsumableInventoryHistory(
        DemoSeedContext seed,
        IReadOnlyList<int> vatInvoiceIds,
        IReadOnlyList<ConsumableInventoryHistoryPlan> plans,
        ICollection<InventoryLog> inventoryLogs)
    {
        foreach (var plan in plans.OrderBy(p => p.Inventory.Id))
        {
            var inboundQuantity = plan.InboundTransfers.Sum(t => t.Quantity);
            var outboundQuantity = plan.OutboundEvents.Sum(t => t.Quantity) + plan.Adjustments.Sum(t => t.Quantity);
            var supplementalImportQuantity = plan.SupplementalImportLots.Sum(lot => lot.RemainingQuantity);
            var baseRemaining = plan.FinalQuantity - inboundQuantity - supplementalImportQuantity;
            if (baseRemaining < 0)
            {
                throw new InvalidOperationException(
                    $"Consumable inventory #{plan.Inventory.Id} cannot fit supplemental seed lots into final quantity.");
            }

            var baseQuantity = Math.Max(1, baseRemaining + outboundQuantity);
            var receivedDate = plan.BaseLot.ReceivedDate ?? seed.StartUtc.AddDays(120 + plan.Inventory.Id % 520);
            var expiredDate = plan.BaseLot.ExpiredDate ?? receivedDate.AddMonths(6 + plan.Inventory.Id % 15);
            var sourceType = string.Equals(plan.BaseLot.SourceType, InventorySourceType.Purchase.ToString(), StringComparison.Ordinal)
                ? InventorySourceType.Purchase.ToString()
                : InventorySourceType.Donation.ToString();
            var sourceId = plan.BaseLot.SourceId ?? plan.Inventory.Id;

            plan.BaseLot.Quantity = baseQuantity;
            plan.BaseLot.RemainingQuantity = baseRemaining;
            plan.BaseLot.ReceivedDate = receivedDate;
            plan.BaseLot.ExpiredDate = expiredDate;
            plan.BaseLot.SourceType = sourceType;
            plan.BaseLot.SourceId = sourceId;
            plan.BaseLot.CreatedAt = receivedDate;
            var latestSeededReceipt = plan.SupplementalImportLots
                .Select(lot => lot.ReceivedDate ?? lot.CreatedAt)
                .Append(receivedDate)
                .Max();
            plan.Inventory.LastStockedAt = plan.InboundTransfers.Count == 0
                ? latestSeededReceipt
                : new[] { latestSeededReceipt, plan.InboundTransfers.Max(t => t.CreatedAt) }.Max();

            inventoryLogs.Add(new InventoryLog
            {
                DepotSupplyInventoryId = plan.Inventory.Id,
                SupplyInventoryLot = plan.BaseLot,
                VatInvoiceId = ResolveVatInvoiceId(vatInvoiceIds, sourceType, sourceId),
                ActionType = InventoryActionType.Import.ToString(),
                QuantityChange = baseQuantity,
                SourceType = sourceType,
                SourceId = sourceId,
                PerformedBy = plan.PerformedBy,
                Note = $"Nhập gốc {plan.ItemModel.Name} vào {plan.Inventory.Depot?.Name ?? $"kho #{plan.Inventory.DepotId}"}",
                ReceivedDate = receivedDate,
                ExpiredDate = expiredDate,
                CreatedAt = receivedDate
            });

            foreach (var supplementalLot in plan.SupplementalImportLots.OrderBy(lot => lot.CreatedAt).ThenBy(lot => lot.SourceId))
            {
                var supplementalReceivedDate = supplementalLot.ReceivedDate ?? supplementalLot.CreatedAt;
                var supplementalExpiredDate = supplementalLot.ExpiredDate;
                var supplementalSourceType = string.Equals(supplementalLot.SourceType, InventorySourceType.Purchase.ToString(), StringComparison.Ordinal)
                    ? InventorySourceType.Purchase.ToString()
                    : InventorySourceType.Donation.ToString();
                var supplementalSourceId = supplementalLot.SourceId ?? plan.Inventory.Id;

                supplementalLot.SourceType = supplementalSourceType;
                supplementalLot.SourceId = supplementalSourceId;
                supplementalLot.ReceivedDate = supplementalReceivedDate;
                supplementalLot.CreatedAt = supplementalReceivedDate;

                inventoryLogs.Add(new InventoryLog
                {
                    DepotSupplyInventoryId = plan.Inventory.Id,
                    SupplyInventoryLot = supplementalLot,
                    ActionType = InventoryActionType.Import.ToString(),
                    QuantityChange = supplementalLot.Quantity,
                    SourceType = supplementalSourceType,
                    SourceId = supplementalSourceId,
                    PerformedBy = plan.PerformedBy,
                    Note = $"Nhập lô demo sắp hết hạn {plan.ItemModel.Name} vào {plan.Inventory.Depot?.Name ?? $"kho #{plan.Inventory.DepotId}"}",
                    ReceivedDate = supplementalReceivedDate,
                    ExpiredDate = supplementalExpiredDate,
                    CreatedAt = supplementalReceivedDate
                });
            }

            foreach (var outbound in plan.OutboundEvents.OrderBy(e => e.CreatedAt))
            {
                inventoryLogs.Add(new InventoryLog
                {
                    DepotSupplyInventoryId = plan.Inventory.Id,
                    SupplyInventoryLot = plan.BaseLot,
                    ActionType = outbound.ActionType,
                    QuantityChange = outbound.Quantity,
                    SourceType = outbound.SourceType,
                    SourceId = outbound.SourceId,
                    MissionId = outbound.MissionId,
                    PerformedBy = outbound.PerformedBy,
                    Note = outbound.Note,
                    ReceivedDate = plan.BaseLot.ReceivedDate,
                    ExpiredDate = plan.BaseLot.ExpiredDate,
                    CreatedAt = outbound.CreatedAt
                });
            }

            foreach (var adjustment in plan.Adjustments.OrderBy(a => a.CreatedAt))
            {
                inventoryLogs.Add(new InventoryLog
                {
                    DepotSupplyInventoryId = plan.Inventory.Id,
                    SupplyInventoryLot = plan.BaseLot,
                    ActionType = InventoryActionType.Adjust.ToString(),
                    QuantityChange = -adjustment.Quantity,
                    SourceType = InventorySourceType.Adjustment.ToString(),
                    PerformedBy = adjustment.PerformedBy,
                    Note = adjustment.Note,
                    ReceivedDate = plan.BaseLot.ReceivedDate,
                    ExpiredDate = plan.BaseLot.ExpiredDate,
                    CreatedAt = adjustment.CreatedAt
                });
            }

            foreach (var inbound in plan.InboundTransfers.OrderBy(t => t.CreatedAt))
            {
                var transferLot = new SupplyInventoryLot
                {
                    SupplyInventoryId = plan.Inventory.Id,
                    Quantity = inbound.Quantity,
                    RemainingQuantity = inbound.Quantity,
                    ReceivedDate = inbound.ReceivedDate,
                    ExpiredDate = inbound.ExpiredDate,
                    SourceType = InventorySourceType.Transfer.ToString(),
                    SourceId = inbound.SourceId,
                    CreatedAt = inbound.CreatedAt
                };

                seed.Lots.Add(transferLot);
                _db.SupplyInventoryLots.Add(transferLot);

                inventoryLogs.Add(new InventoryLog
                {
                    DepotSupplyInventoryId = plan.Inventory.Id,
                    SupplyInventoryLot = transferLot,
                    ActionType = InventoryActionType.TransferIn.ToString(),
                    QuantityChange = inbound.Quantity,
                    SourceType = InventorySourceType.Transfer.ToString(),
                    SourceId = inbound.SourceId,
                    PerformedBy = inbound.PerformedBy,
                    Note = inbound.Note,
                    ReceivedDate = inbound.ReceivedDate,
                    ExpiredDate = inbound.ExpiredDate,
                    CreatedAt = inbound.CreatedAt
                });
            }
        }
    }

    private void BuildReusableInventoryHistory(
        DemoSeedContext seed,
        IReadOnlyList<int> vatInvoiceIds,
        ICollection<InventoryLog> inventoryLogs)
    {
        foreach (var reusableItem in seed.ReusableItems.OrderBy(item => item.Id))
        {
            var sourceType = reusableItem.Id % 3 == 0
                ? InventorySourceType.Purchase.ToString()
                : InventorySourceType.Donation.ToString();
            var sourceId = reusableItem.Id % 3 == 0
                ? vatInvoiceIds[(reusableItem.Id - 1) % vatInvoiceIds.Count]
                : reusableItem.Id;
            var createdAt = reusableItem.CreatedAt ?? seed.StartUtc.AddDays(140 + reusableItem.Id % 480);

            inventoryLogs.Add(new InventoryLog
            {
                ReusableItemId = reusableItem.Id,
                VatInvoiceId = sourceType == InventorySourceType.Purchase.ToString()
                    ? sourceId
                    : null,
                ActionType = InventoryActionType.Import.ToString(),
                QuantityChange = 1,
                SourceType = sourceType,
                SourceId = sourceId,
                PerformedBy = ManagerForDepot(seed, reusableItem.DepotId ?? seed.Depots[reusableItem.Id % seed.Depots.Count].Id),
                Note = $"Nhập thiết bị {reusableItem.ItemModel?.Name ?? $"vật phẩm #{reusableItem.ItemModelId}"} vào kho ban đầu",
                ReceivedDate = createdAt,
                CreatedAt = createdAt
            });
        }

        var reusableMissionUnits = seed.ReusableItems
            .Where(item => item.DepotId.HasValue && !string.Equals(item.Status, "Maintenance", StringComparison.Ordinal))
            .OrderBy(item => item.Id)
            .Take(30)
            .ToList();
        var completedMissions = seed.Missions
            .Where(m => string.Equals(m.Status, "Completed", StringComparison.Ordinal))
            .OrderBy(m => m.Id)
            .ToList();

        for (var index = 0; index < reusableMissionUnits.Count && completedMissions.Count > 0; index++)
        {
            var reusableItem = reusableMissionUnits[index];
            var mission = completedMissions[index % completedMissions.Count];
            var performedBy = ManagerForDepot(seed, reusableItem.DepotId!.Value);
            var exportedAt = (mission.StartTime ?? mission.CreatedAt ?? seed.StartUtc).AddMinutes(35 + index);
            var returnedAt = (mission.CompletedAt ?? exportedAt.AddHours(5)).AddMinutes(-20 + index % 6);

            inventoryLogs.Add(new InventoryLog
            {
                ReusableItemId = reusableItem.Id,
                ActionType = InventoryActionType.Export.ToString(),
                QuantityChange = 1,
                SourceType = InventorySourceType.Mission.ToString(),
                SourceId = mission.Id,
                MissionId = mission.Id,
                PerformedBy = performedBy,
                Note = $"Xuất {reusableItem.ItemModel?.Name ?? $"thiết bị #{reusableItem.ItemModelId}"} cho mission #{mission.Id}",
                CreatedAt = exportedAt
            });

            inventoryLogs.Add(new InventoryLog
            {
                ReusableItemId = reusableItem.Id,
                ActionType = InventoryActionType.Return.ToString(),
                QuantityChange = 1,
                SourceType = InventorySourceType.Mission.ToString(),
                SourceId = mission.Id,
                MissionId = mission.Id,
                PerformedBy = performedBy,
                Note = $"Nhận lại {reusableItem.ItemModel?.Name ?? $"thiết bị #{reusableItem.ItemModelId}"} sau mission #{mission.Id}",
                CreatedAt = returnedAt
            });
        }
    }

    private static Guid ManagerForDepot(DemoSeedContext seed, int depotId)
    {
        return seed.Managers[(depotId - 1) % seed.Managers.Count].Id;
    }
}
