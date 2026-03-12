namespace RESQ.Domain.Entities.Logistics;

public class ReliefItemModel
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string TargetGroup { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static ReliefItemModel Create(int categoryId, string name, string unit, string itemType, string targetGroup)
    {
        return new ReliefItemModel
        {
            CategoryId = categoryId,
            Name = name.Trim(),
            Unit = unit.Trim(),
            ItemType = itemType,
            TargetGroup = targetGroup,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}