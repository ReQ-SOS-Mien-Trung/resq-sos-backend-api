namespace RESQ.Application.UseCases.Finance.Commands.ProcessPaymentReturn
{
    public class WebhookDataType
    {
        public long OrderCode { get; init; }
        public int Amount { get; init; }
        public string? Description { get; init; }
        public string? AccountNumber { get; init; }
        public string? Reference { get; init; }
        public string? TransactionDateTime { get; init; }
        public string? Currency { get; init; }
        public string? PaymentLinkId { get; init; }
        public string? Code { get; init; }
        public string? Desc { get; init; }
        public string? CounterAccountBankId { get; init; }
        public string? CounterAccountBankName { get; init; }
        public string? CounterAccountName { get; init; }
        public string? CounterAccountNumber { get; init; }
        public string? VirtualAccountName { get; init; }
        public string? VirtualAccountNumber { get; init; }
    }
}
