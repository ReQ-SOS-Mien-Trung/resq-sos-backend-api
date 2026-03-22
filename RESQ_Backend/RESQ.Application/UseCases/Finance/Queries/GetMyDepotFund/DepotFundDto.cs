namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;

/// <summary>
/// DTO quỹ kho — dùng cho cả Manager xem quỹ mình và Admin xem từng kho.
/// </summary>
public class DepotFundDto
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public decimal Balance { get; set; }

    /// <summary>Hạn mức tối đa kho được phép tự ứng (balance âm). 0 = không cho âm.</summary>
    public decimal MaxAdvanceLimit { get; set; }

    public DateTime? LastUpdatedAt { get; set; }
}
