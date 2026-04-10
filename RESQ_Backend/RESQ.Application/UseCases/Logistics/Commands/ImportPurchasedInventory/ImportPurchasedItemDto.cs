namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedItemDto
{
    public int Row { get; set; }

    /// <summary>
    /// Path A: Reference an existing item model by ID (mutually exclusive with ItemName).
    /// </summary>
    public int? ItemModelId { get; set; }

    /// <summary>
    /// Path B: Create a new item model from metadata (mutually exclusive with ItemModelId).
    /// </summary>
    public string? ItemName { get; set; }
    public string? CategoryCode { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Unit { get; set; }
    public string? ItemType { get; set; }
    public List<string>? TargetGroups { get; set; }

    /// <summary>
    /// Thể tích mỗi đơn vị (dm³). Bắt buộc khi tạo vật phẩm mới (Path B).
    /// Bỏ qua nếu chọn vật phẩm có sẵn (Path A).
    /// </summary>
    public decimal? VolumePerUnit { get; set; }

    /// <summary>
    /// Cân nặng mỗi đơn vị (kg). Bắt buộc khi tạo vật phẩm mới (Path B).
    /// Bỏ qua nếu chọn vật phẩm có sẵn (Path A).
    /// </summary>
    public decimal? WeightPerUnit { get; set; }

    public DateTime? ReceivedDate { get; set; }
    public DateOnly? ExpiredDate { get; set; }
}
