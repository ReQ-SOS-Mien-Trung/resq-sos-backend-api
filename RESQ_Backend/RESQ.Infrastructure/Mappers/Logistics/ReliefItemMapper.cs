using RESQ.Domain.Entities.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Mappers.Logistics;

public static class ItemModelMapper
{
    /// <summary>
    /// Converts a domain record to an Infrastructure entity (without target groups navigation - caller must set those separately).
    /// </summary>
    public static ItemModel ToEntity(ItemModelRecord model)
    {
        return new ItemModel
        {
            Id = model.Id,
            CategoryId = model.CategoryId,
            Name = model.Name,
            Description = model.Description,
            Unit = model.Unit,
            ItemType = model.ItemType,
            VolumePerUnit = model.VolumePerUnit,
            WeightPerUnit = model.WeightPerUnit,
            ImageUrl = model.ImageUrl,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            UpdatedBy = model.UpdatedBy
        };
    }

    public static ItemModelRecord ToDomain(ItemModel entity)
    {
        return new ItemModelRecord
        {
            Id = entity.Id,
            CategoryId = entity.CategoryId ?? 0,
            Name = entity.Name ?? string.Empty,
            Description = entity.Description,
            Unit = entity.Unit ?? string.Empty,
            ItemType = entity.ItemType ?? string.Empty,
            VolumePerUnit = entity.VolumePerUnit ?? 0m,
            WeightPerUnit = entity.WeightPerUnit ?? 0m,
            TargetGroups = entity.TargetGroups?.Select(tg => tg.Name).ToList() ?? new(),
            ImageUrl = entity.ImageUrl,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }
}
