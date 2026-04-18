namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;

/// <summary>
/// DTO quỹ kho - dùng cho cả Manager xem quỹ mình và Admin xem từng kho.
/// </summary>
public class DepotFundDto
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public decimal Balance { get; set; }

    /// <summary>Hạn mức tối đa tổng tiền ứng trước cho kho này. 0 = không cho phép ứng.</summary>
    public decimal AdvanceLimit { get; set; }

    /// <summary>Tổng tiền đã được các cá nhân ứng trước cho kho.</summary>
    public decimal OutstandingAdvanceAmount { get; set; }

    public DateTime? LastUpdatedAt { get; set; }
}
