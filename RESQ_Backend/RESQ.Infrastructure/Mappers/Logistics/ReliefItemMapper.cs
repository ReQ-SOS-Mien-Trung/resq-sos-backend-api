using RESQ.Domain.Entities.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Mappers.Logistics;

public static class ReliefItemMapper
{
    public static ReliefItem ToEntity(ReliefItemModel model)
    {
        return new ReliefItem
        {
            Id = model.Id,
            CategoryId = model.CategoryId,
            Name = model.Name,
            Unit = model.Unit,
            ItemType = model.ItemType,
            TargetGroup = model.TargetGroup,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }

    public static ReliefItemModel ToDomain(ReliefItem entity)
    {
        return new ReliefItemModel
        {
            Id = entity.Id,
            CategoryId = entity.CategoryId ?? 0,
            Name = entity.Name ?? string.Empty,
            Unit = entity.Unit ?? string.Empty,
            ItemType = entity.ItemType ?? string.Empty,
            TargetGroup = entity.TargetGroup ?? string.Empty,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}