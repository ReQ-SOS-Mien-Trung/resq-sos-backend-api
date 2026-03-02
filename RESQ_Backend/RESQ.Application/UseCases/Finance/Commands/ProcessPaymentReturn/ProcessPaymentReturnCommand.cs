using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessPaymentReturn;

public record ProcessPaymentReturnCommand : IRequest<bool>
{
    public WebhookType? WebhookData { get; init; }
}
