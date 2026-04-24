using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Mappers.Logistics;
using RESQ.Infrastructure.Mappers.Resources;
using DepotSupplyInventory = RESQ.Infrastructure.Entities.Logistics.SupplyInventory;
using ReliefItem = RESQ.Infrastructure.Entities.Logistics.ItemModel;

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
        }

        return ItemModelMapper.ToDomain(item);
    }

    public async Task AddOrganizationReliefItemAsync(OrganizationReliefItemModel model, CancellationToken cancellationToken = default)
    {
        var orgReliefRepo = _unitOfWork.GetRepository<OrganizationReliefItem>();
        var orgReliefEntity = OrganizationReliefItemMapper.ToEntity(model);
        await orgReliefRepo.AddAsync(orgReliefEntity);

        var depotRepo = _unitOfWork.GetRepository<Depot>();
        var depotEntity = await depotRepo.GetByPropertyAsync(d => d.Id == model.ReceivedAt, tracked: true);

        if (depotEntity != null)
        {
            var itemModelRepo = _unitOfWork.GetRepository<ReliefItem>();
            var itemModelEntity = await itemModelRepo.GetByPropertyAsync(im => im.Id == model.ItemModelId);
            var volumePerUnit = itemModelEntity?.VolumePerUnit ?? 0m;
            var weightPerUnit = itemModelEntity?.WeightPerUnit ?? 0m;

            var totalVolume = model.Quantity * volumePerUnit;
            var totalWeight = model.Quantity * weightPerUnit;

            var depotModel = DepotMapper.ToDomain(depotEntity);
            depotModel.UpdateUtilization(totalVolume, totalWeight);

            DepotMapper.UpdateEntity(depotEntity, depotModel);
            await depotRepo.UpdateAsync(depotEntity);
        }

        var isReusable = string.Equals(model.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase);

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
                    DepotId = model.ReceivedAt,
                    ItemModelId = model.ItemModelId,
                    Quantity = model.Quantity,
                    MissionReservedQuantity = 0,
                    TransferReservedQuantity = 0,
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
        }

        var lotRepo = _unitOfWork.GetRepository<SupplyInventoryLot>();
        SupplyInventoryLot? lotEntity = null;
        if (!isReusable && inventory != null)
        {
            lotEntity = new SupplyInventoryLot
            {
                SupplyInventory = inventory,
                Quantity = model.Quantity,
                RemainingQuantity = model.Quantity,
                ReceivedDate = model.ReceivedDate,
                ExpiredDate = model.ExpiredDate,
                SourceType = InventorySourceType.Donation.ToString(),
                SourceId = model.OrganizationId,
                CreatedAt = DateTime.UtcNow
            };
            await lotRepo.AddAsync(lotEntity);
        }

        var logRepo = _unitOfWork.GetRepository<InventoryLog>();
        var logEntity = new InventoryLog
        {
            SupplyInventory = inventory,
            SupplyInventoryLot = lotEntity,
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
    }

    public async Task<List<ItemModelRecord>> GetOrCreateReliefItemsBulkAsync(List<ItemModelRecord> models, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<ReliefItem>();
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

        var existingItems = new List<ReliefItem>();
        var newItems = new List<ReliefItem>();

        foreach (var model in uniqueItems)
        {
            var sortedNames = model.TargetGroups.Select(n => n.ToLower()).OrderBy(n => n).ToList();

            var candidates = await repo.AsQueryable()
                .Include(r => r.TargetGroups)
                .Where(r => r.Name == model.Name
                            && r.CategoryId == model.CategoryId
                            && r.Unit == model.Unit
                            && r.ItemType == model.ItemType
                            && r.Description == model.Description)
                .ToListAsync(cancellationToken);

            var existing = candidates.FirstOrDefault(r =>
                r.TargetGroups.Select(tg => tg.Name.ToLower()).OrderBy(n => n).SequenceEqual(sortedNames));

            if (existing != null)
            {
                existingItems.Add(existing);
            }
            else
            {
                newItems.Add(ItemModelMapper.ToEntity(model));
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
                    var targetGroupEntity = allTargetGroups.FirstOrDefault(t =>
                        string.Equals(t.Name, tgName, StringComparison.OrdinalIgnoreCase));
                    if (targetGroupEntity != null)
                    {
                        newItem.TargetGroups.Add(targetGroupEntity);
                    }
                }
            }

            await repo.AddRangeAsync(newItems);
        }

        existingItems.AddRange(newItems);

        foreach (var item in existingItems)
        {
            results.Add(ItemModelMapper.ToDomain(item));
        }

        return results;
    }

    public async Task<List<ItemModelRecord>> CreateReliefItemsBulkAsync(List<ItemModelRecord> models, CancellationToken cancellationToken = default)
    {
        if (models.Count == 0)
        {
            return new List<ItemModelRecord>();
        }

        var repo = _unitOfWork.GetRepository<ReliefItem>();
        var tgRepo = _unitOfWork.GetRepository<RESQ.Infrastructure.Entities.Logistics.TargetGroup>();

        var allTargetGroups = await tgRepo.AsQueryable(tracked: true).ToListAsync(cancellationToken);
        var entities = new List<ReliefItem>(models.Count);

        foreach (var model in models)
        {
            var entity = ItemModelMapper.ToEntity(model);
            foreach (var targetGroupName in model.TargetGroups)
            {
                var targetGroupEntity = allTargetGroups.FirstOrDefault(t =>
                    string.Equals(t.Name, targetGroupName, StringComparison.OrdinalIgnoreCase));
                if (targetGroupEntity != null)
                {
                    entity.TargetGroups.Add(targetGroupEntity);
                }
            }

            entities.Add(entity);
        }

        await repo.AddRangeAsync(entities);

        return entities.Select(ItemModelMapper.ToDomain).ToList();
    }

    public async Task<List<ItemModelRecord>> GetReliefItemsBulkByDefinitionAsync(List<ItemModelRecord> models, CancellationToken cancellationToken = default)
    {
        if (models.Count == 0)
        {
            return new List<ItemModelRecord>();
        }

        var repo = _unitOfWork.GetRepository<ReliefItem>();
        var resolvedItems = new ItemModelRecord[models.Count];
        var modelGroups = models
            .Select((model, index) => new { model, index })
            .GroupBy(x => BuildItemDefinitionKey(x.model))
            .ToList();

        foreach (var modelGroup in modelGroups)
        {
            var sampleModel = modelGroup.First().model;
            var sortedTargetGroups = sampleModel.TargetGroups
                .Select(targetGroup => targetGroup.ToLowerInvariant())
                .OrderBy(targetGroup => targetGroup)
                .ToList();

            var candidates = await repo.AsQueryable()
                .Include(item => item.TargetGroups)
                .Where(item => item.Name == sampleModel.Name
                               && item.CategoryId == sampleModel.CategoryId
                               && item.Unit == sampleModel.Unit
                               && item.ItemType == sampleModel.ItemType
                               && item.Description == sampleModel.Description)
                .OrderByDescending(item => item.Id)
                .ToListAsync(cancellationToken);

            var exactMatches = candidates
                .Where(item => item.TargetGroups
                    .Select(targetGroup => targetGroup.Name.ToLowerInvariant())
                    .OrderBy(targetGroup => targetGroup)
                    .SequenceEqual(sortedTargetGroups))
                .Take(modelGroup.Count())
                .Reverse()
                .ToList();

            if (exactMatches.Count != modelGroup.Count())
            {
                throw new InvalidOperationException("Không thể đối chiếu đầy đủ item model vừa tạo trong lô nhập cứu trợ.");
            }

            var groupIndexes = modelGroup.Select(x => x.index).ToList();
            for (var index = 0; index < groupIndexes.Count; index++)
            {
                resolvedItems[groupIndexes[index]] = ItemModelMapper.ToDomain(exactMatches[index]);
            }
        }

        return resolvedItems.ToList();
    }

    public async Task AddOrganizationReliefItemsBulkAsync(List<OrganizationReliefItemModel> models, CancellationToken cancellationToken = default)
    {
        if (models.Count == 0)
        {
            return;
        }

        var depotId = models[0].ReceivedAt;

        var itemModelRepo = _unitOfWork.GetRepository<ReliefItem>();
        var itemModelIds = models.Select(x => x.ItemModelId).Distinct().ToList();
        var itemModels = await itemModelRepo.GetAllByPropertyAsync(im => itemModelIds.Contains(im.Id));
        var volumeMap = itemModels.ToDictionary(im => im.Id, im => im.VolumePerUnit ?? 0m);
        var weightMap = itemModels.ToDictionary(im => im.Id, im => im.WeightPerUnit ?? 0m);

        var totalVolume = models.Sum(x => x.Quantity * volumeMap.GetValueOrDefault(x.ItemModelId, 0m));
        var totalWeight = models.Sum(x => x.Quantity * weightMap.GetValueOrDefault(x.ItemModelId, 0m));

        var depotRepo = _unitOfWork.GetRepository<Depot>();
        var depotEntity = await depotRepo.GetByPropertyAsync(d => d.Id == depotId, tracked: true);
        if (depotEntity != null)
        {
            var depotModel = DepotMapper.ToDomain(depotEntity);
            depotModel.UpdateUtilization(totalVolume, totalWeight);
            DepotMapper.UpdateEntity(depotEntity, depotModel);
            await depotRepo.UpdateAsync(depotEntity);
        }

        var orgReliefRepo = _unitOfWork.GetRepository<OrganizationReliefItem>();
        var inventoryRepo = _unitOfWork.GetRepository<DepotSupplyInventory>();
        var reusableRepo = _unitOfWork.GetRepository<ReusableItem>();
        var lotRepo = _unitOfWork.GetRepository<SupplyInventoryLot>();
        var logRepo = _unitOfWork.GetRepository<InventoryLog>();
        var categoryRepo = _unitOfWork.GetRepository<Category>();

        var orgReliefEntities = new List<OrganizationReliefItem>();
        var inventoryEntities = new List<DepotSupplyInventory>();
        var reusableEntities = new List<ReusableItem>();
        var lotEntities = new List<SupplyInventoryLot>();
        var logEntities = new List<InventoryLog>();

        var existingInventories = await inventoryRepo.AsQueryable(tracked: true)
            .Where(i => i.DepotId == depotId
                        && i.ItemModelId.HasValue
                        && itemModelIds.Contains(i.ItemModelId.Value))
            .ToListAsync(cancellationToken);

        var inventoriesByItemModelId = existingInventories.ToDictionary(i => i.ItemModelId!.Value);

        foreach (var model in models)
        {
            var isReusable = string.Equals(model.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase);
            orgReliefEntities.Add(OrganizationReliefItemMapper.ToEntity(model));

            if (!isReusable)
            {
                if (!inventoriesByItemModelId.TryGetValue(model.ItemModelId, out var inventory))
                {
                    inventory = new DepotSupplyInventory
                    {
                        DepotId = model.ReceivedAt,
                        ItemModelId = model.ItemModelId,
                        Quantity = 0,
                        MissionReservedQuantity = 0,
                        TransferReservedQuantity = 0,
                        LastStockedAt = DateTime.UtcNow
                    };

                    inventoriesByItemModelId[model.ItemModelId] = inventory;
                    inventoryEntities.Add(inventory);
                }

                inventory.Quantity = (inventory.Quantity ?? 0) + model.Quantity;
                inventory.LastStockedAt = DateTime.UtcNow;

                var lotEntity = new SupplyInventoryLot
                {
                    SupplyInventory = inventory,
                    Quantity = model.Quantity,
                    RemainingQuantity = model.Quantity,
                    ReceivedDate = model.ReceivedDate,
                    ExpiredDate = model.ExpiredDate,
                    SourceType = InventorySourceType.Donation.ToString(),
                    SourceId = model.OrganizationId,
                    CreatedAt = DateTime.UtcNow
                };
                lotEntities.Add(lotEntity);

                logEntities.Add(new InventoryLog
                {
                    SupplyInventory = inventory,
                    SupplyInventoryLot = lotEntity,
                    ActionType = InventoryActionType.Import.ToString(),
                    SourceType = InventorySourceType.Donation.ToString(),
                    QuantityChange = model.Quantity,
                    SourceId = model.OrganizationId,
                    PerformedBy = model.ReceivedBy,
                    Note = model.Notes,
                    ReceivedDate = model.ReceivedDate,
                    ExpiredDate = model.ExpiredDate,
                    CreatedAt = DateTime.UtcNow
                });

                continue;
            }

            for (var unitIndex = 0; unitIndex < model.Quantity; unitIndex++)
            {
                var serial = $"SN-{model.ItemModelId:D5}-{Guid.NewGuid().ToString("N")[..12].ToUpper()}";
                var reusableEntity = new ReusableItem
                {
                    DepotId = model.ReceivedAt,
                    ItemModelId = model.ItemModelId,
                    SerialNumber = serial,
                    Status = "Available",
                    Condition = "Good",
                    Note = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                reusableEntities.Add(reusableEntity);
                logEntities.Add(new InventoryLog
                {
                    ReusableItem = reusableEntity,
                    ActionType = InventoryActionType.Import.ToString(),
                    SourceType = InventorySourceType.Donation.ToString(),
                    QuantityChange = 1,
                    SourceId = model.OrganizationId,
                    PerformedBy = model.ReceivedBy,
                    Note = model.Notes,
                    ReceivedDate = model.ReceivedDate,
                    ExpiredDate = model.ExpiredDate,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await orgReliefRepo.AddRangeAsync(orgReliefEntities);
        if (inventoryEntities.Count > 0)
        {
            await inventoryRepo.AddRangeAsync(inventoryEntities);
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

        var qtyByItemModel = models
            .GroupBy(m => m.ItemModelId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity));

        var categoryIdsByItemModel = itemModels
            .Where(im => im.CategoryId.HasValue)
            .ToDictionary(im => im.Id, im => im.CategoryId!.Value);

        var qtyByCategory = new Dictionary<int, int>();
        foreach (var (itemModelId, quantity) in qtyByItemModel)
        {
            if (categoryIdsByItemModel.TryGetValue(itemModelId, out var categoryId))
            {
                qtyByCategory.TryGetValue(categoryId, out var currentQuantity);
                qtyByCategory[categoryId] = currentQuantity + quantity;
            }
        }

        foreach (var (categoryId, quantity) in qtyByCategory)
        {
            var category = await categoryRepo.GetByPropertyAsync(c => c.Id == categoryId, tracked: true);
            if (category != null)
            {
                category.Quantity = (category.Quantity ?? 0) + quantity;
                category.UpdatedAt = DateTime.UtcNow;
                await categoryRepo.UpdateAsync(category);
            }
        }
    }

    private static string BuildItemDefinitionKey(ItemModelRecord model)
    {
        var normalizedTargetGroups = string.Join("|", model.TargetGroups
            .Select(targetGroup => targetGroup.Trim().ToLowerInvariant())
            .OrderBy(targetGroup => targetGroup));

        return string.Join("::",
            model.CategoryId,
            model.Name.Trim().ToLowerInvariant(),
            (model.Description ?? string.Empty).Trim().ToLowerInvariant(),
            model.Unit.Trim().ToLowerInvariant(),
            model.ItemType.Trim().ToLowerInvariant(),
            normalizedTargetGroups);
    }
}
