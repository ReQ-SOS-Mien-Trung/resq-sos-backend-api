namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotReusableUnits;

public class ReusableUnitDto
{
    /// <summary>ID vật phẩm tái sử dụng (dùng để gọi decommission).</summary>
    public int ItemId { get; set; }

    public int? ItemModelId { get; set; }
    public string ItemModelName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Số serial của đơn vị vật phẩm.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Trạng thái: Available, Reserved, InTransit, InUse, Maintenance, Decommissioned.</summary>
    public string? Status { get; set; }

    /// <summary>Tình trạng: Good, Fair, Poor.</summary>
    public string? Condition { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
