namespace RESQ.Application.UseCases.Finance.Commands.CreateRepaymentTransaction;

public class CreateRepaymentTransactionRequest
{
    public string ContributorName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public List<RepaymentFundAllocationRequest> Repayments { get; set; } = [];
}

public class RepaymentFundAllocationRequest
{
    public int DepotFundId { get; set; }
    public decimal Amount { get; set; }
}
