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

    public async Task<VatInvoiceModel> CreateVatInvoiceAsync(VatInvoiceModel model, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<VatInvoice>();
        var entity = VatInvoiceMapper.ToEntity(model);
        await repo.AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return VatInvoiceMapper.ToDomain(entity);
    }

    public async Task<List<ItemModelRecord>> GetOrCreateReliefItemsBulkAsync(List<ItemModelRecord> models, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<ItemModel>();
        var results = new List<ItemModelRecord>();

        // Nhóm theo các thuộc tính duy nhất để tránh trùng lặp
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

        // Bulk insert các item mới
        if (newItems.Count > 0)
        {
            var tgRepo = _unitOfWork.GetRepository<RESQ.Infrastructure.Entities.Logistics.TargetGroup>();
            var allTargetGroups = await tgRepo.AsQueryable(tracked: true).ToListAsync(cancellationToken);

            foreach (var newItem in newItems)
            {
                var domainModel = uniqueItems.First(m =>
                    m.Name == newItem.Name && m.CategoryId == newItem.CategoryId &&
                    m.Unit == newItem.Unit && m.ItemType == newItem.ItemType &&
                    m.Description == newItem.Description);

                foreach (var tgName in domainModel.TargetGroups)
                {
                    var tgEntity = allTargetGroups.FirstOrDefault(t =>
                        string.Equals(t.Name, tgName, StringComparison.OrdinalIgnoreCase));
                    if (tgEntity != null)
                        newItem.TargetGroups.Add(tgEntity);
                }
            }

            await repo.AddRangeAsync(newItems);
            await _unitOfWork.SaveAsync();
        }

        // Kết hợp existing và new items
        existingItems.AddRange(newItems);

        foreach (var item in existingItems)
        {
            results.Add(ItemModelMapper.ToDomain(item));
        }

        return results;
    }

    public async Task<List<ItemModelRecord>> CreateReliefItemsBulkAsync(List<ItemModelRecord> models, CancellationToken cancellationToken = default)
    {
        if (models.Count == 0) return new List<ItemModelRecord>();

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
                    entity.TargetGroups.Add(tgEntity);
            }
            entities.Add(entity);
        }

        await repo.AddRangeAsync(entities);
        await _unitOfWork.SaveAsync();

        return entities.Select(ItemModelMapper.ToDomain).ToList();
    }

public async Task AddPurchasedInventoryItemsBulkAsync(List<(PurchasedInventoryItemModel model, decimal? unitPrice, string itemType)> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0) return;

        // Business Rule: Check depot capacity before any insert
        var depotId = items[0].model.ReceivedAt;

        // Look up item models to get VolumePerUnit and WeightPerUnit
        var itemModelRepo = _unitOfWork.GetRepository<ItemModel>();
        var itemModelIds = items.Select(x => x.model.ItemModelId).Distinct().ToList();
        var itemModels = await itemModelRepo.GetAllByPropertyAsync(im => itemModelIds.Contains(im.Id));
        var volumeMap = itemModels.ToDictionary(im => im.Id, im => im.VolumePerUnit ?? 0m);
        var weightMap = itemModels.ToDictionary(im => im.Id, im => im.WeightPerUnit ?? 0m);

        var totalVolume = items.Sum(x => x.model.Quantity * (volumeMap.GetValueOrDefault(x.model.ItemModelId, 0m)));
        var totalWeight = items.Sum(x => x.model.Quantity * (weightMap.GetValueOrDefault(x.model.ItemModelId, 0m)));

        var depotRepo = _unitOfWork.GetRepository<Depot>();
        var depotEntity = await depotRepo.GetByPropertyAsync(d => d.Id == depotId, tracked: true);
        if (depotEntity != null)
        {
            var depotModel = DepotMapper.ToDomain(depotEntity);
            // Throws DepotCapacityExceededException if over limit
            depotModel.UpdateUtilization(totalVolume, totalWeight);
            DepotMapper.UpdateEntity(depotEntity, depotModel);
            await depotRepo.UpdateAsync(depotEntity);
        }

        var inventoryRepo      = _unitOfWork.GetRepository<SupplyInventory>();
        var reusableRepo       = _unitOfWork.GetRepository<ReusableItem>();
        var logRepo            = _unitOfWork.GetRepository<InventoryLog>();
        var vatInvoiceItemRepo = _unitOfWork.GetRepository<VatInvoiceItem>();

        var newInventoryEntities   = new List<SupplyInventory>(); // only truly-new consumable rows
        var reusableEntities       = new List<ReusableItem>();
        var logEntities            = new List<InventoryLog>();
        var vatInvoiceItemEntities = new List<VatInvoiceItem>();

        // Track reusable entities alongside their source item for per-unit log creation after save
        var reusableEntityWithItem = new List<(ReusableItem Entity, PurchasedInventoryItemModel Model, int? VatInvoiceId)>();
        // Map item index → log index for consumable rows (to set DepotSupplyInventoryId after save)
        var consumableItemToLogIndex = new Dictionary<int, int>();

        for (int itemIdx = 0; itemIdx < items.Count; itemIdx++)
        {
            var (model, unitPrice, itemType) = items[itemIdx];
            var isReusable = string.Equals(itemType, "Reusable", StringComparison.OrdinalIgnoreCase);

            // 1a. Consumable → cập nhật/tạo bản ghi SupplyInventory
            if (!isReusable)
            {
                var existingInventory = await inventoryRepo.GetByPropertyAsync(
                    i => i.DepotId == model.ReceivedAt && i.ItemModelId == model.ItemModelId,
                    tracked: true);

                if (existingInventory != null)
                {
                    existingInventory.Quantity      = (existingInventory.Quantity ?? 0) + model.Quantity;
                    existingInventory.LastStockedAt = DateTime.UtcNow;
                }
                else
                {
                    newInventoryEntities.Add(new SupplyInventory
                    {
                        DepotId                   = model.ReceivedAt,
                        ItemModelId               = model.ItemModelId,
                        Quantity                  = model.Quantity,
                        MissionReservedQuantity   = 0,
                        TransferReservedQuantity  = 0,
                        LastStockedAt             = DateTime.UtcNow
                    });
                }

                // 2a. Log for consumable: 1 log per item row (QuantityChange = N)
                consumableItemToLogIndex[itemIdx] = logEntities.Count;
                logEntities.Add(new InventoryLog
                {
                    VatInvoiceId   = model.VatInvoiceId,
                    ActionType     = InventoryActionType.Import.ToString(),
                    SourceType     = InventorySourceType.Purchase.ToString(),
                    QuantityChange = model.Quantity,
                    SourceId       = null,
                    PerformedBy    = model.ReceivedBy,
                    Note           = model.Notes,
                    ReceivedDate   = model.ReceivedDate,
                    ExpiredDate    = model.ExpiredDate,
                    CreatedAt      = DateTime.UtcNow
                });
            }

            // 1b. Reusable → tạo N bản ghi ReusableItem (serial number do system sinh).
            // Log được tạo sau khi save để gắn ReusableItemId chính xác.
            if (isReusable)
            {
                for (int u = 0; u < model.Quantity; u++)
                {
                    var serial = $"SN-{model.ItemModelId:D5}-{Guid.NewGuid().ToString("N")[..12].ToUpper()}";
                    var reusableEntity = new ReusableItem
                    {
                        DepotId      = model.ReceivedAt,
                        ItemModelId  = model.ItemModelId,
                        SerialNumber = serial,
                        Status       = "Available",
                        Condition    = "Good",
                        Note         = null,
                        CreatedAt    = DateTime.UtcNow,
                        UpdatedAt    = DateTime.UtcNow
                    };
                    reusableEntities.Add(reusableEntity);
                    reusableEntityWithItem.Add((reusableEntity, model, model.VatInvoiceId));
                }
            }

            // 3. Tạo VatInvoiceItem - lưu giá từng dòng trong hóa đơn VAT (1 per item row)
            vatInvoiceItemEntities.Add(new VatInvoiceItem
            {
                VatInvoiceId = model.VatInvoiceId,
                ItemModelId  = model.ItemModelId,
                Quantity     = model.Quantity,
                UnitPrice    = unitPrice,
                CreatedAt    = DateTime.UtcNow
            });
        }

        // -- Persist ----------------------------------------------------------

        await vatInvoiceItemRepo.AddRangeAsync(vatInvoiceItemEntities);
        await _unitOfWork.SaveAsync();

        if (newInventoryEntities.Count > 0)
        {
            await inventoryRepo.AddRangeAsync(newInventoryEntities);
            await _unitOfWork.SaveAsync();
        }

        // Bulk insert reusable item records, then create 1 log per unit (with ReusableItemId)
        if (reusableEntities.Count > 0)
        {
            await reusableRepo.AddRangeAsync(reusableEntities);
            await _unitOfWork.SaveAsync();

            // Create individual log per reusable unit so each unit is fully traceable by ReusableItemId
            foreach (var (entity, model, vatInvoiceId) in reusableEntityWithItem)
            {
                logEntities.Add(new InventoryLog
                {
                    ReusableItemId = entity.Id,
                    VatInvoiceId   = vatInvoiceId,
                    ActionType     = InventoryActionType.Import.ToString(),
                    SourceType     = InventorySourceType.Purchase.ToString(),
                    QuantityChange = 1,
                    SourceId       = null,
                    PerformedBy    = model.ReceivedBy,
                    Note           = model.Notes,
                    ReceivedDate   = model.ReceivedDate,
                    ExpiredDate    = model.ExpiredDate,
                    CreatedAt      = DateTime.UtcNow
                });
            }
        }

        // Gắn DepotSupplyInventoryId cho log của consumable item + tạo lot
        var lotRepo = _unitOfWork.GetRepository<SupplyInventoryLot>();
        var lotEntities = new List<SupplyInventoryLot>();
        var lotToLogMapping = new List<(int lotIndex, int logIndex)>();

        for (int i = 0; i < items.Count; i++)
        {
            var (model, _, itemType) = items[i];
            var isReusable = string.Equals(itemType, "Reusable", StringComparison.OrdinalIgnoreCase);
            if (!isReusable && consumableItemToLogIndex.TryGetValue(i, out var logIdx))
            {
                var inv = newInventoryEntities.FirstOrDefault(x =>
                              x.DepotId == model.ReceivedAt && x.ItemModelId == model.ItemModelId)
                          ?? await inventoryRepo.GetByPropertyAsync(
                              x => x.DepotId == model.ReceivedAt && x.ItemModelId == model.ItemModelId);
                if (inv != null)
                {
                    logEntities[logIdx].DepotSupplyInventoryId = inv.Id;

                    // Create lot for this purchase batch
                    lotToLogMapping.Add((lotEntities.Count, logIdx));
                    lotEntities.Add(new SupplyInventoryLot
                    {
                        SupplyInventoryId = inv.Id,
                        Quantity          = model.Quantity,
                        RemainingQuantity = model.Quantity,
                        ReceivedDate      = model.ReceivedDate,
                        ExpiredDate       = model.ExpiredDate,
                        SourceType        = InventorySourceType.Purchase.ToString(),
                        SourceId          = model.VatInvoiceId,
                        CreatedAt         = DateTime.UtcNow
                    });
                }
            }
            // Reusable: log per unit already created above with ReusableItemId
        }

        // Bulk insert lots and link to logs
        if (lotEntities.Count > 0)
        {
            await lotRepo.AddRangeAsync(lotEntities);
            await _unitOfWork.SaveAsync();

            foreach (var (lotIdx, logIdx) in lotToLogMapping)
                logEntities[logIdx].SupplyInventoryLotId = lotEntities[lotIdx].Id;
        }

        await logRepo.AddRangeAsync(logEntities);
        await _unitOfWork.SaveAsync();

        // -- Cập nhật Category.Quantity ----------------------------------------
        // Gom tổng quantity nhập theo ItemModelId, rồi tra CategoryId, rồi cộng vào Category.Quantity
        // Reuse itemModelRepo from capacity check above
        var categoryRepo  = _unitOfWork.GetRepository<Category>();

        var qtyByItemModel = items
            .GroupBy(x => x.model.ItemModelId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.model.Quantity));

        var qtyByCategory = new Dictionary<int, int>();
        foreach (var (itemModelId, qty) in qtyByItemModel)
        {
            var im = await itemModelRepo.GetByPropertyAsync(x => x.Id == itemModelId, tracked: false);
            if (im?.CategoryId != null)
            {
                qtyByCategory.TryGetValue(im.CategoryId.Value, out var current);
                qtyByCategory[im.CategoryId.Value] = current + qty;
            }
        }

        foreach (var (catId, qty) in qtyByCategory)
        {
            var cat = await categoryRepo.GetByPropertyAsync(c => c.Id == catId, tracked: true);
            if (cat != null)
            {
                cat.Quantity  = (cat.Quantity ?? 0) + qty;
                cat.UpdatedAt = DateTime.UtcNow;
                await categoryRepo.UpdateAsync(cat);
            }
        }
        await _unitOfWork.SaveAsync();
    }
}
