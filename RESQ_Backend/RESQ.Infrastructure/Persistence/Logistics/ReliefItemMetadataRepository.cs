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

namespace RESQ.Infrastructure.Persistence.Logistics;

public class ItemModelMetadataRepository(IUnitOfWork unitOfWork) : IItemModelMetadataRepository
{
    private static readonly Dictionary<string, string> ItemTypeVietnamese = new()
    {
        ["Consumable"] = "Tiêu hao",
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
            from ri in _unitOfWork.GetRepository<ItemModel>().AsQueryable()
            join cat in _unitOfWork.GetRepository<Category>().AsQueryable() on ri.CategoryId equals cat.Id
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
            ItemTypeDisplay: im.ItemType != null && ItemTypeVietnamese.TryGetValue(im.ItemType, out var vn)
                ? vn
                : im.ItemType ?? string.Empty,
            Unit: im.Unit ?? string.Empty,
            Description: im.Description ?? string.Empty
        )).ToList();
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
}
