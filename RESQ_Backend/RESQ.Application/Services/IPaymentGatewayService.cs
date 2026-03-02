using RESQ.Domain.Entities.Finance;
using RESQ.Application.Common.Models;

namespace RESQ.Application.Services;

public interface IPaymentGatewayService
{
    /// <summary>
    /// Creates a payment link/order with the payment provider.
    /// </summary>
    Task<PaymentLinkResult> CreatePaymentLinkAsync(DonationModel donation, CancellationToken cancellationToken = default);
}
