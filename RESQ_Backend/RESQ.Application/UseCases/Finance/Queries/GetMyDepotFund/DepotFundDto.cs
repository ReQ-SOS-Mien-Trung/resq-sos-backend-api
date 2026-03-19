namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;

/// <summary>
/// DTO quỹ kho — dùng cho cả Manager xem quỹ mình và Admin xem từng kho.
/// </summary>
public class DepotFundDto
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public decimal Balance { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}
