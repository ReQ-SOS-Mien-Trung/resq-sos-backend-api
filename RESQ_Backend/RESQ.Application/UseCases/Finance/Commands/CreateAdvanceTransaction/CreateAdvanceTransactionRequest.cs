namespace RESQ.Application.UseCases.Finance.Commands.CreateAdvanceTransaction;

/// <summary>
/// Request body cho endpoint tạo giao dịch ứng trước.
/// </summary>
public class CreateAdvanceTransactionRequest
{
    public decimal Amount { get; set; }
    public string ContributorName { get; set; } = string.Empty;
    public Guid? ContributorId { get; set; }
}
