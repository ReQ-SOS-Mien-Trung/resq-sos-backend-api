using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Mappers.Logistics;
using RESQ.Infrastructure.Mappers.Resources;
using ReliefItem = RESQ.Infrastructure.Entities.Logistics.ItemModel;
using DepotSupplyInventory = RESQ.Infrastructure.Entities.Logistics.SupplyInventory;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class OrganizationReliefRepository(IUnitOfWork unitOfWork) : IOrganizationReliefRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<ItemModelRecord> GetOrCreateReliefItemAsync(ItemModelRecord model, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<ItemModel>();

        var item = await repo.GetByPropertyAsync(
            r => r.Name == model.Name && r.CategoryId == model.CategoryId, 
            tracked: true);

        if (item == null)
        {
            item = ItemModelMapper.ToEntity(model);
            await repo.AddAsync(item);
            await _unitOfWork.SaveAsync();
        }

        return ItemModelMapper.ToDomain(item);
    }

    public async Task AddOrganizationReliefItemAsync(OrganizationReliefItemModel model, CancellationToken cancellationToken = default)
    {
        // 1. Log the organization reception using Mapper
        var orgReliefRepo = _unitOfWork.GetRepository<OrganizationReliefItem>();
        var orgReliefEntity = OrganizationReliefItemMapper.ToEntity(model);
        await orgReliefRepo.AddAsync(orgReliefEntity);

        // 2. Map Entity to DepotModel to enforce Domain Capacity Rules!
        var depotRepo = _unitOfWork.GetRepository<Depot>();
        var depotEntity = await depotRepo.GetByPropertyAsync(d => d.Id == model.ReceivedAt, tracked: true);
        
        if (depotEntity != null)
        {
            var depotModel = DepotMapper.ToDomain(depotEntity);
            
            // This applies Domain Rule: Throws DepotCapacityExceededException if over limit!
            depotModel.UpdateUtilization(model.Quantity); 
            
            DepotMapper.UpdateEntity(depotEntity, depotModel);
            await depotRepo.UpdateAsync(depotEntity);
        }

        var isReusable = string.Equals(model.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase);

        // 3. Add or Update to Depot Supply Inventory (consumable only)
        var inventoryRepo = _unitOfWork.GetRepository<SupplyInventory>();
        SupplyInventory? inventory = null;
        if (!isReusable)
        {
            inventory = await inventoryRepo.GetByPropertyAsync(
                i => i.DepotId == model.ReceivedAt && i.ItemModelId == model.ItemModelId,
                tracked: true);

            if (inventory == null)
            {
                inventory = new SupplyInventory
                {
                    DepotId                  = model.ReceivedAt,
                    ItemModelId              = model.ItemModelId,
                    Quantity                 = model.Quantity,
                    MissionReservedQuantity  = 0,
                    TransferReservedQuantity = 0,
                    LastStockedAt            = DateTime.UtcNow
                };
                await inventoryRepo.AddAsync(inventory);
            }
            else
            {
                inventory.Quantity = (inventory.Quantity ?? 0) + model.Quantity;
                inventory.LastStockedAt = DateTime.UtcNow;
                await inventoryRepo.UpdateAsync(inventory);
            }

            await _unitOfWork.SaveAsync();
        }

        // 4. Create inventory lot for consumable items only
        var lotRepo = _unitOfWork.GetRepository<SupplyInventoryLot>();
        SupplyInventoryLot? lotEntity = null;
        if (!isReusable && inventory != null)
        {
            lotEntity = new SupplyInventoryLot
            {
                SupplyInventoryId = inventory.Id,
                Quantity = model.Quantity,
                RemainingQuantity = model.Quantity,
                ReceivedDate = model.ReceivedDate,
                ExpiredDate = model.ExpiredDate,
                SourceType = InventorySourceType.Donation.ToString(),
                SourceId = model.OrganizationId,
                CreatedAt = DateTime.UtcNow
            };
            await lotRepo.AddAsync(lotEntity);
            await _unitOfWork.SaveAsync();
        }

        // 5. Create an Inventory Action Log
        var logRepo = _unitOfWork.GetRepository<InventoryLog>();
        var logEntity = new InventoryLog
        {
            DepotSupplyInventoryId = inventory?.Id,
            SupplyInventoryLotId = lotEntity?.Id,
            
            // UPDATED: Using PascalCase enum 'Import'
            ActionType = InventoryActionType.Import.ToString(),
            SourceType = InventorySourceType.Donation.ToString(), 
            
            QuantityChange = model.Quantity,
            SourceId = model.OrganizationId,
            PerformedBy = model.ReceivedBy,
            Note = model.Notes,
            ReceivedDate = model.ReceivedDate,
            ExpiredDate = model.ExpiredDate,
            CreatedAt = DateTime.UtcNow 
        };
        
        await logRepo.AddAsync(logEntity);
        await _unitOfWork.SaveAsync();
    }

    public async Task<List<ItemModelRecord>> GetOrCreateReliefItemsBulkAsync(List<ItemModelRecord> models, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<ReliefItem>();
        var results = new List<ItemModelRecord>();

        // Group by unique combination to avoid duplicates
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

        var existingItems = new List<ReliefItem>();
        var newItems = new List<ReliefItem>();

        foreach (var model in uniqueItems)
        {
            var sortedNames = model.TargetGroups.Select(n => n.ToLower()).OrderBy(n => n).ToList();

            // Load candidate existing items (same Name/Category/Unit/ItemType) with their TargetGroups
            var candidates = await repo.AsQueryable()
                .Include(r => r.TargetGroups)
                .Where(r => r.Name == model.Name && r.CategoryId == model.CategoryId &&
                            r.Unit == model.Unit && r.ItemType == model.ItemType &&
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

        // Bulk insert new items
        if (newItems.Count > 0)
        {
            // Resolve TargetGroup entities from DB and attach
            var tgRepo = _unitOfWork.GetRepository<RESQ.Infrastructure.Entities.Logistics.TargetGroup>();
            var allTargetGroups = await tgRepo.AsQueryable(tracked: true).ToListAsync(cancellationToken);

            foreach (var newItem in newItems)
            {
                // Find the matching domain model to get TargetGroups names
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

        // Combine existing and new items
        existingItems.AddRange(newItems);

        // Convert back to domain models
        foreach (var item in existingItems)
        {
            results.Add(ItemModelMapper.ToDomain(item));
        }

        return results;
    }

    public async Task<List<ItemModelRecord>> CreateReliefItemsBulkAsync(List<ItemModelRecord> models, CancellationToken cancellationToken = default)
    {
        if (models.Count == 0) return new List<ItemModelRecord>();

        var repo = _unitOfWork.GetRepository<ReliefItem>();
        var tgRepo = _unitOfWork.GetRepository<RESQ.Infrastructure.Entities.Logistics.TargetGroup>();

        var allTargetGroups = await tgRepo.AsQueryable(tracked: true).ToListAsync(cancellationToken);
        var entities = new List<ReliefItem>(models.Count);

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

    public async Task AddOrganizationReliefItemsBulkAsync(List<OrganizationReliefItemModel> models, CancellationToken cancellationToken = default)
    {
        if (models.Count == 0) return;

        var orgReliefRepo = _unitOfWork.GetRepository<OrganizationReliefItem>();
        var inventoryRepo = _unitOfWork.GetRepository<DepotSupplyInventory>();
        var reusableRepo  = _unitOfWork.GetRepository<ReusableItem>();
        var logRepo = _unitOfWork.GetRepository<InventoryLog>();

        var orgReliefEntities = new List<OrganizationReliefItem>();
        var inventoryEntities = new List<DepotSupplyInventory>();
        var reusableEntities  = new List<ReusableItem>();
        var logEntities = new List<InventoryLog>();

        // Process all models to prepare bulk data
        foreach (var model in models)
        {
            var isReusable = string.Equals(model.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase);

            // 1. Create OrganizationReliefItem entity
            var orgReliefEntity = OrganizationReliefItemMapper.ToEntity(model);
            orgReliefEntities.Add(orgReliefEntity);

            if (!isReusable)
            {
                // 2a. Consumable → Find or create SupplyInventory record
                var existingInventory = await inventoryRepo.GetByPropertyAsync(
                    i => i.DepotId == model.ReceivedAt && i.ItemModelId == model.ItemModelId,
                    tracked: true);

                if (existingInventory != null)
                {
                    existingInventory.Quantity = (existingInventory.Quantity ?? 0) + model.Quantity;
                    existingInventory.LastStockedAt = DateTime.UtcNow;
                }
                else
                {
                    inventoryEntities.Add(new DepotSupplyInventory
                    {
                        DepotId                  = model.ReceivedAt,
                        ItemModelId              = model.ItemModelId,
                        Quantity                 = model.Quantity,
                        MissionReservedQuantity  = 0,
                        TransferReservedQuantity = 0,
                        LastStockedAt            = DateTime.UtcNow
                    });
                }
            }

            if (isReusable)
            {
                // 2b. Reusable → create N individual ReusableItem records
                for (int u = 0; u < model.Quantity; u++)
                {
                    var serial = $"SN-{model.ItemModelId:D5}-{Guid.NewGuid().ToString("N")[..12].ToUpper()}";
                    reusableEntities.Add(new ReusableItem
                    {
                        DepotId      = model.ReceivedAt,
                        ItemModelId  = model.ItemModelId,
                        SerialNumber = serial,
                        Status       = "Available",
                        Condition    = "Good",
                        Note         = null,
                        CreatedAt    = DateTime.UtcNow,
                        UpdatedAt    = DateTime.UtcNow
                    });
                }
            }

            // 3. Prepare log entry
            var logEntity = new InventoryLog
            {
                ActionType = InventoryActionType.Import.ToString(),
                SourceType = InventorySourceType.Donation.ToString(),
                QuantityChange = model.Quantity,
                SourceId = model.OrganizationId,
                PerformedBy = model.ReceivedBy,
                Note = model.Notes,
                ReceivedDate = model.ReceivedDate,
                ExpiredDate = model.ExpiredDate,
                CreatedAt = DateTime.UtcNow
            };
            logEntities.Add(logEntity);
        }

        // Bulk insert organization relief items
        await orgReliefRepo.AddRangeAsync(orgReliefEntities);
        await _unitOfWork.SaveAsync();

        // Bulk insert new consumable inventory records
        if (inventoryEntities.Count > 0)
        {
            await inventoryRepo.AddRangeAsync(inventoryEntities);
            await _unitOfWork.SaveAsync();
        }

        // Bulk insert reusable item records
        if (reusableEntities.Count > 0)
        {
            await reusableRepo.AddRangeAsync(reusableEntities);
            await _unitOfWork.SaveAsync();
        }

        // Update log entities with consumable inventory IDs, create lots, and bulk insert
        var lotRepo = _unitOfWork.GetRepository<SupplyInventoryLot>();
        var lotEntities = new List<SupplyInventoryLot>();
        var lotToLogMapping = new List<(int lotIndex, int logIndex)>();

        for (int i = 0; i < models.Count; i++)
        {
            var model = models[i];
            var isReusable = string.Equals(model.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase);

            if (!isReusable)
            {
                var inventory = inventoryEntities.FirstOrDefault(inv =>
                    inv.DepotId == model.ReceivedAt && inv.ItemModelId == model.ItemModelId) ??
                    await inventoryRepo.GetByPropertyAsync(
                        inv => inv.DepotId == model.ReceivedAt && inv.ItemModelId == model.ItemModelId);

                if (inventory != null)
                {
                    logEntities[i].DepotSupplyInventoryId = inventory.Id;

                    // Create lot for this import batch
                    lotToLogMapping.Add((lotEntities.Count, i));
                    lotEntities.Add(new SupplyInventoryLot
                    {
                        SupplyInventoryId = inventory.Id,
                        Quantity = model.Quantity,
                        RemainingQuantity = model.Quantity,
                        ReceivedDate = model.ReceivedDate,
                        ExpiredDate = model.ExpiredDate,
                        SourceType = InventorySourceType.Donation.ToString(),
                        SourceId = model.OrganizationId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
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

        // ── Cập nhật Category.Quantity (tổng toàn hệ thống) ──────────────────
        var itemModelRepo = _unitOfWork.GetRepository<ReliefItem>();
        var categoryRepo  = _unitOfWork.GetRepository<Category>();

        var qtyByItemModel = models
            .GroupBy(m => m.ItemModelId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity));

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
