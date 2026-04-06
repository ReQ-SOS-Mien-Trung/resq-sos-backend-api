using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Mappers.Logistics;
using TargetGroupEntity = RESQ.Infrastructure.Entities.Logistics.TargetGroup;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class ItemModelMetadataRepository(IUnitOfWork unitOfWork) : IItemModelMetadataRepository
{
    private static readonly Dictionary<string, string> ItemTypeVietnamese = new()
    {
        ["Consumable"] = "Tiêu thụ",
        ["Reusable"]   = "Tái sử dụng"
    };

    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<List<MetadataDto>> GetAllForMetadataAsync(CancellationToken cancellationToken = default)
    {
        var items = await _unitOfWork.GetRepository<ItemModel>()
            .GetAllByPropertyAsync(r => true);

        return items
            .OrderBy(r => r.Id)
            .Select(r => new MetadataDto
            {
                Key   = r.Id.ToString(),
                Value = r.Name ?? string.Empty
            })
            .ToList();
    }

    public async Task<List<MetadataDto>> GetByCategoryCodeAsync(
        ItemCategoryCode categoryCode,
        CancellationToken cancellationToken = default)
    {
        var categoryCodeString = categoryCode.ToString();

        var items = await (
            from ri in _unitOfWork.Set<ItemModel>()
            join cat in _unitOfWork.Set<Category>() on ri.CategoryId equals cat.Id
            where cat.Code == categoryCodeString
            orderby ri.Id
            select new MetadataDto
            {
                Key = ri.Id.ToString(),
                Value = ri.Name ?? string.Empty
            }
        ).ToListAsync(cancellationToken);

        return items;
    }

    public async Task<List<DonationImportItemInfo>> GetAllForDonationTemplateAsync(
        CancellationToken cancellationToken = default)
    {
        var itemModels = await _unitOfWork.GetRepository<ItemModel>()
            .AsQueryable()
            .Include(im => im.TargetGroups)
            .Include(im => im.Category)
            .Where(im => im.CategoryId != null)
            .OrderBy(im => im.CategoryId)
            .ThenBy(im => im.Id)
            .ToListAsync(cancellationToken);

        return itemModels.Select(im => new DonationImportItemInfo(
            Id: im.Id,
            Name: im.Name ?? string.Empty,
            CategoryCode: im.Category?.Code ?? string.Empty,
            TargetGroupDisplay: TargetGroupTranslations.JoinAsVietnamese(
                im.TargetGroups.Select(tg => tg.Name)),
            TargetGroupRaw: string.Join(", ", im.TargetGroups.Select(tg => tg.Name)),
            ItemTypeDisplay: im.ItemType != null && ItemTypeVietnamese.TryGetValue(im.ItemType, out var vn)
                ? vn
                : im.ItemType ?? string.Empty,
            ItemTypeRaw: im.ItemType ?? string.Empty,
            Unit: im.Unit ?? string.Empty,
            Description: im.Description ?? string.Empty
        )).ToList();
    }

    public async Task<List<DonationImportTargetGroupInfo>> GetAllTargetGroupsForTemplateAsync(
        CancellationToken cancellationToken = default)
    {
        var groups = await _unitOfWork.GetRepository<TargetGroupEntity>()
            .AsQueryable()
            .OrderBy(tg => tg.Id)
            .ToListAsync(cancellationToken);

        return groups
            .Select(tg => new DonationImportTargetGroupInfo(
                Id: tg.Id,
                Name: tg.Name,
                NameDisplay: TargetGroupTranslations.ToVietnamese(tg.Name)))
            .ToList();
    }

    public async Task<Dictionary<int, ItemModelRecord>> GetByIdsAsync(
        IReadOnlyList<int> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Count == 0)
            return new Dictionary<int, ItemModelRecord>();

        var distinctIds = ids.Distinct().ToList();
        var result = new Dictionary<int, ItemModelRecord>(distinctIds.Count);

        // Chunk at 500 to avoid SQL parameter limits
        foreach (var chunk in distinctIds.Chunk(500))
        {
            var entities = await _unitOfWork.GetRepository<ItemModel>()
                .AsQueryable()
                .AsNoTracking()
                .Include(im => im.TargetGroups)
                .Where(im => chunk.Contains(im.Id))
                .ToListAsync(cancellationToken);

            foreach (var entity in entities)
            {
                // Indexer (not .Add) to safely handle any duplicate key edge-case
                result[entity.Id] = ItemModelMapper.ToDomain(entity);
            }
        }

        return result;
    }

    public async Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.GetRepository<Category>()
            .AsQueryable()
            .AnyAsync(x => x.Id == categoryId, cancellationToken);
    }

    public async Task<bool> HasInventoryTransactionsAsync(int itemModelId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.GetRepository<InventoryLog>()
            .AsQueryable()
            .AnyAsync(x =>
                (x.SupplyInventory != null && x.SupplyInventory.ItemModelId == itemModelId) ||
                (x.ReusableItem != null && x.ReusableItem.ItemModelId == itemModelId),
                cancellationToken);
    }

    public async Task<bool> UpdateItemModelAsync(ItemModelRecord model, CancellationToken cancellationToken = default)
    {
        var itemRepo = _unitOfWork.GetRepository<ItemModel>();
        var targetGroupRepo = _unitOfWork.GetRepository<TargetGroupEntity>();

        var entity = await itemRepo.AsQueryable(tracked: true)
            .Include(x => x.TargetGroups)
            .FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);

        if (entity == null)
            return false;

        var normalizedTargetGroups = (model.TargetGroups ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var targetGroups = await targetGroupRepo.AsQueryable(tracked: true)
            .Where(tg => normalizedTargetGroups.Contains(tg.Name))
            .ToListAsync(cancellationToken);

        if (targetGroups.Count != normalizedTargetGroups.Count)
            throw new InvalidOperationException("Một hoặc nhiều target group không tồn tại trong hệ thống.");

        entity.CategoryId = model.CategoryId;
        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.Unit = model.Unit;
        entity.ItemType = model.ItemType;
        entity.ImageUrl = model.ImageUrl;
        entity.UpdatedAt = model.UpdatedAt ?? DateTime.UtcNow;

        entity.TargetGroups.Clear();
        foreach (var targetGroup in targetGroups)
        {
            entity.TargetGroups.Add(targetGroup);
        }

        await _unitOfWork.SaveAsync();
        return true;
    }
}
