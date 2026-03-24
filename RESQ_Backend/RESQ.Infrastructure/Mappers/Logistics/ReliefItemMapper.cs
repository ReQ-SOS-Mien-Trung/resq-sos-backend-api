using RESQ.Domain.Entities.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Mappers.Logistics;

public static class ItemModelMapper
{
    /// <summary>
    /// Converts a domain record to an Infrastructure entity (without target groups navigation — caller must set those separately).
    /// </summary>
    public static ItemModel ToEntity(ItemModelRecord model)
    {
        return new ItemModel
        {
            Id = model.Id,
            CategoryId = model.CategoryId,
            Name = model.Name,
            Unit = model.Unit,
            ItemType = model.ItemType,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }

    public static ItemModelRecord ToDomain(ItemModel entity)
    {
        return new ItemModelRecord
        {
            Id = entity.Id,
            CategoryId = entity.CategoryId ?? 0,
            Name = entity.Name ?? string.Empty,
            Unit = entity.Unit ?? string.Empty,
            ItemType = entity.ItemType ?? string.Empty,
            TargetGroups = entity.TargetGroups?.Select(tg => tg.Name).ToList() ?? new(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
