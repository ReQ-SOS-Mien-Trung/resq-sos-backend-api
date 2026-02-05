using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Mappers.Logistics;

public static class ItemCategoryMapper
{
    public static ItemCategory ToEntity(ItemCategoryModel model)
    {
        return new ItemCategory
        {
            Id = model.Id,
            Code = model.Code.ToString(), // Persist Enum name (e.g., "Food")
            Name = model.Name,
            Description = model.Description,
            Quantity = model.Quantity,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }

    public static void UpdateEntity(ItemCategory entity, ItemCategoryModel model)
    {
        entity.Code = model.Code.ToString();
        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.UpdatedAt = model.UpdatedAt;
    }

    public static ItemCategoryModel ToDomain(ItemCategory entity)
    {
        // Handle parsing string back to Enum safely
        if (!Enum.TryParse<ItemCategoryCode>(entity.Code, ignoreCase: true, out var code))
        {
            code = ItemCategoryCode.Others; // Fallback or handle error
        }

        return new ItemCategoryModel
        {
            Id = entity.Id,
            Code = code,
            Name = entity.Name ?? string.Empty,
            Quantity = entity.Quantity ?? 0,
            Description = entity.Description ?? string.Empty,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
