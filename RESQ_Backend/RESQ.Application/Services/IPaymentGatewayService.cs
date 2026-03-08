using RESQ.Domain.Entities.Finance;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Commands.ProcessPayosPaymentReturn;

namespace RESQ.Application.Services;

public interface IPaymentGatewayService
{
    Task<PaymentLinkResult> CreatePaymentLinkAsync(DonationModel donation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the webhook signature using the raw JSON body to avoid binding artifacts.
    /// </summary>
    /// <param name="jsonBody">The exact raw JSON body received from the request</param>
    /// <returns>True if valid, False otherwise</returns>
    bool VerifyWebhookSignature(string jsonBody);
}

