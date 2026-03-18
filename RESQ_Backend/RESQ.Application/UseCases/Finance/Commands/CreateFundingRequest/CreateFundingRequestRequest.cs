using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.CreateFundingRequest;

public class CreateFundingRequestRequest
{
    [Required]
    public int DepotId { get; set; }

    public string? Description { get; set; }

    /// <summary>URL file ảnh/PDF hóa đơn hoặc file kế hoạch đã upload lên storage.</summary>
    public string? AttachmentUrl { get; set; }

    /// <summary>
    /// Danh sách vật tư dự kiến mua. TotalAmount sẽ được tính tự động = sum(items[].totalPrice).
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

    public string? Unit { get; set; }

    [Required]
    public int Quantity { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    [Required]
    public decimal TotalPrice { get; set; }

    [Required]
    public string ItemType { get; set; } = string.Empty;

    [Required]
    public string TargetGroup { get; set; } = string.Empty;

    public DateOnly? ReceivedDate { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public string? Notes { get; set; }
}