using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.CreateFundingRequest;

/// <summary>
/// [Cách 2] Depot gửi yêu cầu cấp thêm quỹ kèm danh sách vật tư.
/// TotalAmount được tính tự động = sum(items[].TotalPrice).
/// </summary>
public record CreateFundingRequestCommand(
    int DepotId,
    string? Description,
    string? AttachmentUrl,
    List<FundingRequestItemDto> Items,
    Guid RequestedBy
) : IRequest<int>;

public class FundingRequestItemDto
{
    public int Row { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string TargetGroup { get; set; } = string.Empty;
    public DateOnly? ReceivedDate { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public string? Notes { get; set; }
}
