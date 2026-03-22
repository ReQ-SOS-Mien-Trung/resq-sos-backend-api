namespace RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

/// <summary>
/// DTO item trong danh sách quỹ tất cả kho — dành cho Admin.
/// </summary>
public class DepotFundListItemDto
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public decimal Balance { get; set; }

    /// <summary>Hạn mức tối đa kho được phép tự ứng (balance âm). 0 = không cho âm.</summary>
    public decimal MaxAdvanceLimit { get; set; }
    
    /// <summary>null nếu kho chưa từng được cấp quỹ.</summary>
    public DateTime? LastUpdatedAt { get; set; }
}
