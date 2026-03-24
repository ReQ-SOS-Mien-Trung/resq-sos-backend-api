namespace RESQ.Domain.Entities.Logistics;

public class ItemModelRecord
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public List<string> TargetGroups { get; set; } = new();
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static ItemModelRecord Create(int categoryId, string name, string unit, string itemType, List<string> targetGroups)
    {
        return new ItemModelRecord
        {
            CategoryId = categoryId,
            Name = name.Trim(),
            Unit = unit.Trim(),
            ItemType = itemType,
            TargetGroups = targetGroups,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
