namespace RESQ.Application.UseCases.Finance.Commands.CreateRepaymentTransaction;

/// <summary>
/// Request body cho endpoint tạo giao dịch hoàn trả tiền ứng trước.
/// </summary>
public class CreateRepaymentTransactionRequest
{
    public decimal Amount { get; set; }
    public string ContributorName { get; set; } = string.Empty;
    public Guid? ContributorId { get; set; }
}
