using MediatR;
using RESQ.Application.Common.Models.Finance.ZaloPay;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessZaloPayPayment;

public record ProcessZaloPayPaymentCommand : IRequest<bool>
{
    public ZaloPayCallbackRequest CallbackData { get; init; } = null!;
}
