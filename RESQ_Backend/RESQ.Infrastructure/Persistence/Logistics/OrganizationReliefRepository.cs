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

        // 3. Add or Update to Depot Supply Inventory
        var inventoryRepo = _unitOfWork.GetRepository<SupplyInventory>();
        var inventory = await inventoryRepo.GetByPropertyAsync(
            i => i.DepotId == model.ReceivedAt && i.ItemModelId == model.ItemModelId, 
            tracked: true);

        if (inventory == null)
        {
            inventory = new SupplyInventory
            {
                DepotId = model.ReceivedAt,
                ItemModelId = model.ItemModelId,
                Quantity = model.Quantity,
                ReservedQuantity = 0,
                LastStockedAt = DateTime.UtcNow
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

        // 4. Create an Inventory Action Log
        var logRepo = _unitOfWork.GetRepository<InventoryLog>();
        var logEntity = new InventoryLog
        {
            DepotSupplyInventoryId = inventory.Id,
            
            // UPDATED: Using PascalCase enum 'Import'
            ActionType = InventoryActionType.Import.ToString(),
            SourceType = InventorySourceType.Donation.ToString(), 
            
            QuantityChange = model.Quantity,
            SourceId = model.OrganizationId,
            PerformedBy = model.ReceivedBy,
            Note = model.Notes,
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
            .GroupBy(m => new { m.Name, m.CategoryId, m.Unit, m.ItemType, m.TargetGroup })
            .Select(g => g.First())
            .ToList();

        var existingItems = new List<ReliefItem>();
        var newItems = new List<ReliefItem>();

        foreach (var model in uniqueItems)
        {
            var existing = await repo.GetByPropertyAsync(
                r => r.Name == model.Name && r.CategoryId == model.CategoryId && 
                     r.Unit == model.Unit && r.ItemType == model.ItemType && 
                     r.TargetGroup == model.TargetGroup, 
                tracked: true);

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

    public async Task AddOrganizationReliefItemsBulkAsync(List<OrganizationReliefItemModel> models, CancellationToken cancellationToken = default)
    {
        if (models.Count == 0) return;

        var orgReliefRepo = _unitOfWork.GetRepository<OrganizationReliefItem>();
        var inventoryRepo = _unitOfWork.GetRepository<DepotSupplyInventory>();
        var logRepo = _unitOfWork.GetRepository<InventoryLog>();

        var orgReliefEntities = new List<OrganizationReliefItem>();
        var inventoryEntities = new List<DepotSupplyInventory>();
        var logEntities = new List<InventoryLog>();

        // Process all models to prepare bulk data
        foreach (var model in models)
        {
            // 1. Create OrganizationReliefItem entity
            var orgReliefEntity = OrganizationReliefItemMapper.ToEntity(model);
            orgReliefEntities.Add(orgReliefEntity);

            // 2. Find or create inventory record
            var existingInventory = await inventoryRepo.GetByPropertyAsync(
                i => i.DepotId == model.ReceivedAt && i.ItemModelId == model.ItemModelId,
                tracked: true);

            DepotSupplyInventory inventory;
            if (existingInventory != null)
            {
                existingInventory.Quantity = (existingInventory.Quantity ?? 0) + model.Quantity;
                existingInventory.LastStockedAt = DateTime.UtcNow;
                inventory = existingInventory;
            }
            else
            {
                inventory = new DepotSupplyInventory
                {
                    DepotId = model.ReceivedAt,
                    ItemModelId = model.ItemModelId,
                    Quantity = model.Quantity,
                    ReservedQuantity = 0,
                    LastStockedAt = DateTime.UtcNow
                };
                inventoryEntities.Add(inventory);
            }

            // 3. Prepare log entry (will be created after inventory is saved)
            var logEntity = new InventoryLog
            {
                ActionType = InventoryActionType.Import.ToString(),
                SourceType = InventorySourceType.Donation.ToString(),
                QuantityChange = model.Quantity,
                SourceId = model.OrganizationId,
                PerformedBy = model.ReceivedBy,
                Note = model.Notes,
                CreatedAt = DateTime.UtcNow
            };
            logEntities.Add(logEntity);
        }

        // Bulk insert organization relief items
        await orgReliefRepo.AddRangeAsync(orgReliefEntities);
        await _unitOfWork.SaveAsync();

        // Bulk insert new inventory records
        if (inventoryEntities.Count > 0)
        {
            await inventoryRepo.AddRangeAsync(inventoryEntities);
            await _unitOfWork.SaveAsync();
        }

        // Update log entities with inventory IDs and bulk insert
        for (int i = 0; i < models.Count; i++)
        {
            var model = models[i];
            var inventory = inventoryEntities.FirstOrDefault(inv => 
                inv.DepotId == model.ReceivedAt && inv.ItemModelId == model.ItemModelId) ??
                await inventoryRepo.GetByPropertyAsync(
                    inv => inv.DepotId == model.ReceivedAt && inv.ItemModelId == model.ItemModelId);
                    
            if (inventory != null)
            {
                logEntities[i].DepotSupplyInventoryId = inventory.Id;
            }
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
