using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.CreateFundingRequest;

public class CreateFundingRequestRequest
{
    public string? Description { get; set; }

    /// <summary>
    /// Danh sách v?t ph?m d? ki?n mua. TotalAmount s? du?c tính t? d?ng = sum(items[].totalPrice).
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

    /// <summary>Mô t? v?t ph?m - tuong ?ng c?t G trong template Excel.</summary>
    public string? Description { get; set; }

    /// <summary>URL ?nh v?t ph?m (optional). Ch? áp d?ng khi t?o item model m?i (theo tên).</summary>
    public string? ImageUrl { get; set; }

    [Required]
    public int Quantity { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    /// <summary>Th? tích m?i don v? (dm³). N?u không truy?n, m?c d?nh = 0.</summary>
    public decimal? VolumePerUnit { get; set; }

    /// <summary>Cân n?ng m?i don v? (kg). N?u không truy?n, m?c d?nh = 0.</summary>
    public decimal? WeightPerUnit { get; set; }
}
