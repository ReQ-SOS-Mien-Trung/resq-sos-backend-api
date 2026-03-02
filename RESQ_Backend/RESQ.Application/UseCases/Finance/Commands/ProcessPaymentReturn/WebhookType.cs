namespace RESQ.Application.UseCases.Finance.Commands.ProcessPaymentReturn
{
    public class WebhookType
    {
        public string? Code { get; init; }
        public string? Desc { get; init; }
        public bool Success { get; init; }
        public WebhookDataType? Data { get; init; }
        public string? Signature { get; init; }
    }
}
