using RESQ.Domain.Enum.Logistics;

namespace RESQ.Domain.Entities.Logistics;

public class ItemCategoryModel
{
    public int Id { get; set; }
    public ItemCategoryCode Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ItemCategoryModel() { }

    public static ItemCategoryModel Create(ItemCategoryCode code, string name, string description)
    {
        return new ItemCategoryModel
        {
            Code = code,
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Quantity = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null 
        };
    }

    public void Update(ItemCategoryCode code, string name, string description)
    {
        Code = code;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }
}
