namespace RESQ.Domain.Entities.Logistics;

public class ItemModelRecord
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public decimal VolumePerUnit { get; set; }
    public decimal WeightPerUnit { get; set; }
    public List<string> TargetGroups { get; set; } = new();
    public string? ImageUrl { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Người cập nhật item model gần nhất.</summary>
    public Guid? UpdatedBy { get; set; }

    public static ItemModelRecord Create(int categoryId, string name, string unit, string itemType, List<string> targetGroups, decimal volumePerUnit = 0, decimal weightPerUnit = 0, string? description = null)
    {
        return new ItemModelRecord
        {
            CategoryId = categoryId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Unit = unit.Trim(),
            ItemType = itemType,
            VolumePerUnit = volumePerUnit,
            WeightPerUnit = weightPerUnit,
            TargetGroups = targetGroups,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
