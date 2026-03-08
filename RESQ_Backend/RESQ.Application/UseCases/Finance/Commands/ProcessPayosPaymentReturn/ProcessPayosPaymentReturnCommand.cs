using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessPayosPaymentReturn;

public record ProcessPayosPaymentReturnCommand : IRequest<bool>
{
    public WebhookType? WebhookData { get; init; }
}

