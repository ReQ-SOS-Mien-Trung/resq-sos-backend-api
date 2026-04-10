using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.CreateFundingRequest;

/// <summary>
/// [Cách 2] Depot gửi yêu cầu cấp thêm quỹ kèm danh sách vật tư.
/// DepotId được tự động lấy từ manager đang đăng nhập.
/// TotalAmount được tính tự động = sum(items[].TotalPrice).
/// </summary>
public record CreateFundingRequestCommand(
    string? Description,
    List<FundingRequestItemDto> Items,
    Guid RequestedBy
) : IRequest<int>;

public class FundingRequestItemDto
{
    public int Row { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string TargetGroup { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? VolumePerUnit { get; set; }
    public decimal? WeightPerUnit { get; set; }
}
