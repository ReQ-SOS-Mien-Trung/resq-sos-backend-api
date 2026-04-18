using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Mappers.Logistics;

public static class ItemCategoryMapper
{
    public static Category ToEntity(ItemCategoryModel model)
    {
        return new Category
        {
            Id = model.Id,
            Code = model.Code.ToString(), // Persist Enum name (e.g., "Food")
            Name = model.Name,
            Description = model.Description,
            Quantity = model.Quantity,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            CreatedBy = model.CreatedBy,
            UpdatedBy = model.UpdatedBy
        };
    }

    public static void UpdateEntity(Category entity, ItemCategoryModel model)
    {
        entity.Code = model.Code.ToString();
        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.UpdatedAt = model.UpdatedAt;
        entity.UpdatedBy = model.UpdatedBy;
    }

    // Explicit mapping from legacy DB string codes to enum values.
    // Existing DB rows may still store codes in uppercase snake_case ("MEDICINE", "REPAIR_TOOLS")
    // from before the seed was corrected. Enum.TryParse alone cannot bridge that gap.
    private static readonly Dictionary<string, ItemCategoryCode> DbCodeOverrides =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["FOOD"]              = ItemCategoryCode.Food,
            ["WATER"]             = ItemCategoryCode.Water,
            ["MEDICINE"]          = ItemCategoryCode.Medical,
            ["HYGIENE"]           = ItemCategoryCode.Hygiene,
            ["CLOTHING"]          = ItemCategoryCode.Clothing,
            ["SHELTER"]           = ItemCategoryCode.Shelter,
            ["REPAIR_TOOLS"]      = ItemCategoryCode.RepairTools,
            ["RESCUE_EQUIPMENT"]  = ItemCategoryCode.RescueEquipment,
            ["HEATING"]           = ItemCategoryCode.Heating,
            ["VEHICLE"]           = ItemCategoryCode.Vehicle,
            ["OTHERS"]            = ItemCategoryCode.Others,
        };

    public static ItemCategoryModel ToDomain(Category entity)
    {
        // Try the legacy override map first (covers old uppercase/snake_case DB codes),
        // then fall back to Enum.TryParse for new PascalCase codes, then default to Others.
        if (!DbCodeOverrides.TryGetValue(entity.Code ?? string.Empty, out var code)
            && !Enum.TryParse<ItemCategoryCode>(entity.Code, ignoreCase: true, out code))
        {
            code = ItemCategoryCode.Others;
        }

        return new ItemCategoryModel
        {
            Id = entity.Id,
            Code = code,
            Name = entity.Name ?? string.Empty,
            Quantity = entity.Quantity ?? 0,
            Description = entity.Description ?? string.Empty,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy
        };
    }
}
