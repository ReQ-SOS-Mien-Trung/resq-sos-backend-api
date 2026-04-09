using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

/// <summary>
/// DTO item trong danh sách quỹ kho — dùng cho cả Admin (xem tất cả) và Manager (xem quỹ mình).
/// </summary>
public class DepotFundListItemDto
{
    /// <summary>ID của bản ghi quỹ — dùng để chọn quỹ khi nhập hàng (import-purchase).</summary>
    public int Id { get; set; }
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public decimal Balance { get; set; }

    /// <summary>Hạn mức tối đa kho được phép tự ứng (balance âm). 0 = không cho âm.</summary>
    public decimal MaxAdvanceLimit { get; set; }

    /// <summary>Loại nguồn quỹ: Campaign / SystemFund / null (legacy).</summary>
    public FundSourceType? FundSourceType { get; set; }

    /// <summary>Tên nguồn quỹ (tên chiến dịch hoặc "Quỹ hệ thống").</summary>
    public string? FundSourceName { get; set; }

    /// <summary>null nếu kho chưa từng được cấp quỹ.</summary>
    public DateTime? LastUpdatedAt { get; set; }
}
