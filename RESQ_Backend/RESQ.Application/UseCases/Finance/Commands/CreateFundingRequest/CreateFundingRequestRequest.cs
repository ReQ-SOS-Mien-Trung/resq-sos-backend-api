using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.CreateFundingRequest;

public class CreateFundingRequestRequest
{
    public string? Description { get; set; }

    /// <summary>
    /// Danh sách vật phẩm dự kiến mua. TotalAmount sẽ được tính tự động = sum(items[].totalPrice).
    /// </summary>
    [Required]
    public List<FundingRequestItemRequest> Items { get; set; } = [];
}

public class FundingRequestItemRequest
{
    public int Row { get; set; }

    [Required]
    public string ItemName { get; set; } = string.Empty;

    [Required]
    public string CategoryCode { get; set; } = string.Empty;

    [Required]
    public string TargetGroup { get; set; } = string.Empty;

    [Required]
    public string ItemType { get; set; } = string.Empty;

    public string? Unit { get; set; }

    /// <summary>Mô tả vật phẩm - tương ứng cột G trong template Excel.</summary>
    public string? Description { get; set; }

    /// <summary>URL ảnh vật phẩm (optional). Chỉ áp dụng khi tạo item model mới (theo tên).</summary>
    public string? ImageUrl { get; set; }

    [Required]
    public int Quantity { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    /// <summary>Thể tích mỗi đơn vị (dm³). Nếu không truyền, mặc định = 0.</summary>
    public decimal? VolumePerUnit { get; set; }

    /// <summary>Cân nặng mỗi đơn vị (kg). Nếu không truyền, mặc định = 0.</summary>
    public decimal? WeightPerUnit { get; set; }
}
