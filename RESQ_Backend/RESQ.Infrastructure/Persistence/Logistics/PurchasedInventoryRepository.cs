using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Mappers.Logistics;
using RESQ.Infrastructure.Mappers.Resources;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class PurchasedInventoryRepository(IUnitOfWork unitOfWork) : IPurchasedInventoryRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<bool> ExistsBySerialAndNumberAsync(string invoiceSerial, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<VatInvoice>();
        var existing = await repo.GetByPropertyAsync(
            v => v.InvoiceSerial == invoiceSerial && v.InvoiceNumber == invoiceNumber,
            tracked: false);
        return existing != null;
    }

    public async Task<TrackedEntityReference<VatInvoiceModel>> CreateVatInvoiceAsync(
        VatInvoiceModel model,
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<VatInvoice>();
        var entity = VatInvoiceMapper.ToEntity(model);
        await repo.AddAsync(entity);

        return new TrackedEntityReference<VatInvoiceModel>(
            VatInvoiceMapper.ToDomain(entity),
            () => entity.Id);
    }

    public async Task<List<ItemModelRecord>> GetOrCreateReliefItemsBulkAsync(
        List<ItemModelRecord> models,
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<ItemModel>();
        var results = new List<ItemModelRecord>();

        var uniqueItems = models
            .GroupBy(m => new
            {
                m.Name,
                m.Description,
                m.CategoryId,
                m.Unit,
                m.ItemType,
                TargetGroupsKey = string.Join(",", m.TargetGroups.OrderBy(x => x))
            })
            .Select(g => g.First())
            .ToList();

        var existingItems = new List<ItemModel>();
        var newItems = new List<ItemModel>();

        foreach (var model in uniqueItems)
        {
            var sortedNames = model.TargetGroups.Select(n => n.ToLower()).OrderBy(n => n).ToList();

            var candidates = await repo.AsQueryable(tracked: true)
                .Include(r => r.TargetGroups)
                .Where(r => r.Name == model.Name &&
                            r.CategoryId == model.CategoryId &&
                            r.Unit == model.Unit &&
                            r.ItemType == model.ItemType &&
                            r.Description == model.Description)
                .ToListAsync(cancellationToken);

            var existing = candidates.FirstOrDefault(r =>
                r.TargetGroups.Select(tg => tg.Name.ToLower()).OrderBy(n => n).SequenceEqual(sortedNames));

            if (existing != null)
            {
                existingItems.Add(existing);
            }
            else
            {
                var newItem = ItemModelMapper.ToEntity(model);
                newItems.Add(newItem);
            }
        }

        if (newItems.Count > 0)
        {
            var tgRepo = _unitOfWork.GetRepository<RESQ.Infrastructure.Entities.Logistics.TargetGroup>();
            var allTargetGroups = await tgRepo.AsQueryable(tracked: true).ToListAsync(cancellationToken);

            foreach (var newItem in newItems)
            {
                var domainModel = uniqueItems.First(m =>
                    m.Name == newItem.Name
                    && m.CategoryId == newItem.CategoryId
                    && m.Unit == newItem.Unit
                    && m.ItemType == newItem.ItemType
                    && m.Description == newItem.Description);

                foreach (var tgName in domainModel.TargetGroups)
                {
                    var tgEntity = allTargetGroups.FirstOrDefault(t =>
                        string.Equals(t.Name, tgName, StringComparison.OrdinalIgnoreCase));
                    if (tgEntity != null)
                    {
                        newItem.TargetGroups.Add(tgEntity);
                    }
                }
            }

            await repo.AddRangeAsync(newItems);
        }

        existingItems.AddRange(newItems);
        results.AddRange(existingItems.Select(ItemModelMapper.ToDomain));
        return results;
    }

    public async Task<List<TrackedEntityReference<ItemModelRecord>>> CreateReliefItemsBulkAsync(
        List<ItemModelRecord> models,
        CancellationToken cancellationToken = default)
    {
        if (models.Count == 0)
        {
            return [];
        }

        var repo = _unitOfWork.GetRepository<ItemModel>();
        var tgRepo = _unitOfWork.GetRepository<RESQ.Infrastructure.Entities.Logistics.TargetGroup>();

        var allTargetGroups = await tgRepo.AsQueryable(tracked: true).ToListAsync(cancellationToken);
        var entities = new List<ItemModel>(models.Count);

        foreach (var model in models)
        {
            var entity = ItemModelMapper.ToEntity(model);
            foreach (var tgName in model.TargetGroups)
            {
                var tgEntity = allTargetGroups.FirstOrDefault(t =>
                    string.Equals(t.Name, tgName, StringComparison.OrdinalIgnoreCase));
                if (tgEntity != null)
                {
                    entity.TargetGroups.Add(tgEntity);
                }
            }

            entities.Add(entity);
        }

        await repo.AddRangeAsync(entities);

        return entities
            .Select(entity => new TrackedEntityReference<ItemModelRecord>(
                ItemModelMapper.ToDomain(entity),
                () => entity.Id))
            .ToList();
    }

    public async Task AddPurchasedInventoryItemsBulkAsync(
        List<(PurchasedInventoryItemModel model, decimal? unitPrice, string itemType)> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        var depotId = items[0].model.ReceivedAt;
        var itemModelRepo = _unitOfWork.GetRepository<ItemModel>();
        var itemModelIds = items.Select(x => x.model.ItemModelId).Distinct().ToList();
        var itemModels = await itemModelRepo.GetAllByPropertyAsync(im => itemModelIds.Contains(im.Id));
        var volumeMap = itemModels.ToDictionary(im => im.Id, im => im.VolumePerUnit ?? 0m);
        var weightMap = itemModels.ToDictionary(im => im.Id, im => im.WeightPerUnit ?? 0m);

        var totalVolume = items.Sum(x => x.model.Quantity * volumeMap.GetValueOrDefault(x.model.ItemModelId, 0m));
        var totalWeight = items.Sum(x => x.model.Quantity * weightMap.GetValueOrDefault(x.model.ItemModelId, 0m));

        var depotRepo = _unitOfWork.GetRepository<Depot>();
        var depotEntity = await depotRepo.GetByPropertyAsync(d => d.Id == depotId, tracked: true);
        if (depotEntity != null)
        {
            var depotModel = DepotMapper.ToDomain(depotEntity);
            depotModel.UpdateUtilization(totalVolume, totalWeight);
            DepotMapper.UpdateEntity(depotEntity, depotModel);
            await depotRepo.UpdateAsync(depotEntity);
        }

        var inventoryRepo = _unitOfWork.GetRepository<SupplyInventory>();
        var reusableRepo = _unitOfWork.GetRepository<ReusableItem>();
        var logRepo = _unitOfWork.GetRepository<InventoryLog>();
        var vatInvoiceItemRepo = _unitOfWork.GetRepository<VatInvoiceItem>();
        var lotRepo = _unitOfWork.GetRepository<SupplyInventoryLot>();
        var categoryRepo = _unitOfWork.GetRepository<Category>();

        var allItemModelIds = items
            .Select(x => x.model.ItemModelId)
            .Distinct()
            .ToList();

        var existingInventories = allItemModelIds.Count == 0
            ? []
            : await inventoryRepo.AsQueryable(tracked: true)
                .Where(i => i.DepotId == depotId
                    && i.ItemModelId.HasValue
                    && allItemModelIds.Contains(i.ItemModelId.Value))
                .ToListAsync(cancellationToken);
        var batchLoggedAt = DateTime.UtcNow;

        var inventoryByItemModelId = existingInventories
            .Where(x => x.ItemModelId.HasValue)
            .ToDictionary(x => x.ItemModelId!.Value, x => x);

        var newInventoryEntities = new List<SupplyInventory>();
        var reusableEntities = new List<ReusableItem>();
        var lotEntities = new List<SupplyInventoryLot>();
        var logEntities = new List<InventoryLog>();
        var vatInvoiceItemEntities = new List<VatInvoiceItem>();
        var reusableImportLogsByInvoiceAndItemModel = new Dictionary<(int VatInvoiceId, int ItemModelId), InventoryLog>();

        foreach (var (model, unitPrice, itemType) in items)
        {
            var isReusable = string.Equals(itemType, ItemType.Reusable.ToString(), StringComparison.OrdinalIgnoreCase);

            vatInvoiceItemEntities.Add(new VatInvoiceItem
            {
                VatInvoiceId = model.VatInvoiceId,
                ItemModelId = model.ItemModelId,
                Quantity = model.Quantity,
                UnitPrice = unitPrice,
                CreatedAt = batchLoggedAt
            });

            if (!inventoryByItemModelId.TryGetValue(model.ItemModelId, out var inventory))
            {
                inventory = new SupplyInventory
                {
                    DepotId = model.ReceivedAt,
                    ItemModelId = model.ItemModelId,
                    Quantity = 0,
                    MissionReservedQuantity = 0,
                    TransferReservedQuantity = 0,
                    LastStockedAt = DateTime.UtcNow
                };

                inventoryByItemModelId[model.ItemModelId] = inventory;
                newInventoryEntities.Add(inventory);
            }

            inventory.Quantity = (inventory.Quantity ?? 0) + model.Quantity;
            inventory.LastStockedAt = DateTime.UtcNow;

            if (!isReusable)
            {
                var lot = new SupplyInventoryLot
                {
                    SupplyInventory = inventory,
                    Quantity = model.Quantity,
                    RemainingQuantity = model.Quantity,
                    ReceivedDate = model.ReceivedDate,
                    ExpiredDate = model.ExpiredDate,
                    SourceType = InventorySourceType.Purchase.ToString(),
                    SourceId = model.VatInvoiceId,
                    CreatedAt = batchLoggedAt
                };
                lotEntities.Add(lot);

                logEntities.Add(new InventoryLog
                {
                    SupplyInventory = inventory,
                    SupplyInventoryLot = lot,
                    ItemModelId = model.ItemModelId,
                    VatInvoiceId = model.VatInvoiceId,
                    ActionType = InventoryActionType.Import.ToString(),
                    SourceType = InventorySourceType.Purchase.ToString(),
                    QuantityChange = model.Quantity,
                    SourceId = null,
                    PerformedBy = model.ReceivedBy,
                    Note = model.Notes,
                    ReceivedDate = model.ReceivedDate,
                    ExpiredDate = model.ExpiredDate,
                    CreatedAt = batchLoggedAt
                });

                continue;
            }

            for (var unitIndex = 0; unitIndex < model.Quantity; unitIndex++)
            {
                var reusableEntity = new ReusableItem
                {
                    DepotId = model.ReceivedAt,
                    ItemModelId = model.ItemModelId,
                    SerialNumber = $"SN-{model.ItemModelId:D5}-{Guid.NewGuid().ToString("N")[..12].ToUpper()}",
                    Status = ReusableItemStatus.Available.ToString(),
                    Condition = ReusableItemCondition.Good.ToString(),
                    Note = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                reusableEntities.Add(reusableEntity);
            }

            var reusableLogKey = (model.VatInvoiceId, model.ItemModelId);
            if (!reusableImportLogsByInvoiceAndItemModel.TryGetValue(reusableLogKey, out var reusableImportLog))
            {
                reusableImportLog = new InventoryLog
                {
                    SupplyInventory = inventory,
                    ItemModelId = model.ItemModelId,
                    VatInvoiceId = model.VatInvoiceId,
                    ActionType = InventoryActionType.Import.ToString(),
                    SourceType = InventorySourceType.Purchase.ToString(),
                    QuantityChange = 0,
                    SourceId = null,
                    PerformedBy = model.ReceivedBy,
                    Note = model.Notes,
                    ReceivedDate = model.ReceivedDate,
                    ExpiredDate = null,
                    CreatedAt = batchLoggedAt
                };

                reusableImportLogsByInvoiceAndItemModel[reusableLogKey] = reusableImportLog;
                logEntities.Add(reusableImportLog);
            }

            reusableImportLog.QuantityChange = (reusableImportLog.QuantityChange ?? 0) + model.Quantity;
        }

        if (vatInvoiceItemEntities.Count > 0)
        {
            await vatInvoiceItemRepo.AddRangeAsync(vatInvoiceItemEntities);
        }
        if (newInventoryEntities.Count > 0)
        {
            await inventoryRepo.AddRangeAsync(newInventoryEntities);
        }
        if (reusableEntities.Count > 0)
        {
            await reusableRepo.AddRangeAsync(reusableEntities);
        }
        if (lotEntities.Count > 0)
        {
            await lotRepo.AddRangeAsync(lotEntities);
        }
        if (logEntities.Count > 0)
        {
            await logRepo.AddRangeAsync(logEntities);
        }

        var qtyByItemModel = items
            .GroupBy(x => x.model.ItemModelId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.model.Quantity));

        var qtyByCategory = new Dictionary<int, int>();
        foreach (var (itemModelId, qty) in qtyByItemModel)
        {
            var itemModel = itemModels.FirstOrDefault(x => x.Id == itemModelId)
                ?? await itemModelRepo.GetByPropertyAsync(x => x.Id == itemModelId, tracked: false);

            if (itemModel?.CategoryId == null)
            {
                continue;
            }

            qtyByCategory.TryGetValue(itemModel.CategoryId.Value, out var current);
            qtyByCategory[itemModel.CategoryId.Value] = current + qty;
        }

        if (qtyByCategory.Count == 0)
        {
            return;
        }

        var categories = await categoryRepo.AsQueryable(tracked: true)
            .Where(c => qtyByCategory.Keys.Contains(c.Id))
            .ToListAsync(cancellationToken);

        foreach (var (categoryId, qty) in qtyByCategory)
        {
            var category = categories.FirstOrDefault(c => c.Id == categoryId);
            if (category == null)
            {
                continue;
            }

            category.Quantity = (category.Quantity ?? 0) + qty;
            category.UpdatedAt = DateTime.UtcNow;
        }
    }
}
