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

    /// <summary>Người tạo danh mục.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Người cập nhật danh mục gần nhất.</summary>
    public Guid? UpdatedBy { get; set; }

    public ItemCategoryModel() { }

    public static ItemCategoryModel Create(ItemCategoryCode code, string name, string description, Guid? createdBy = null)
    {
        return new ItemCategoryModel
        {
            Code = code,
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Quantity = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
            CreatedBy = createdBy
        };
    }

    public void Update(ItemCategoryCode code, string name, string description, Guid? updatedBy = null)
    {
        Code = code;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
