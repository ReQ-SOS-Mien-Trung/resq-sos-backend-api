using RESQ.Domain.Entities.Finance;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.Models.Finance.ZaloPay;
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

    /// <summary>
    /// Queries the payment gateway for the current status of an order.
    /// Only implemented by gateways that support order status queries (e.g. ZaloPay).
    /// </summary>
    /// <param name="appTransId">The merchant's transaction ID</param>
    /// <param name="cancellationToken"></param>
    /// <returns>ZaloPayQueryResponse, or null if not supported / on error</returns>
    Task<ZaloPayQueryResponse?> QueryOrderAsync(string appTransId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the payment gateway for the current status of a payment link by its ID.
    /// Implemented by PayOS; returns null for other gateways.
    /// ReturnCode mapping: 1 = PAID, 2 = failed/cancelled, 3 = processing.
    /// Callers MUST NOT overwrite <c>Donation.TransactionId</c> from the result
    /// (use the existing stored value instead).
    /// </summary>
    /// <param name="paymentLinkId">The payment link / transaction ID stored on the donation</param>
    Task<ZaloPayQueryResponse?> QueryPaymentLinkAsync(string paymentLinkId, CancellationToken cancellationToken = default)
        => Task.FromResult<ZaloPayQueryResponse?>(null);
}

