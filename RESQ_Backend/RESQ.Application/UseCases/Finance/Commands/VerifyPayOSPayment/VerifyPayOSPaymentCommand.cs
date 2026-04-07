using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.VerifyPayOSPayment;

/// <summary>
/// Fallback command: when the PayOS webhook does not reliably update the DB
/// (e.g. webhook URL points to a different server / old deployment),
/// the frontend calls <c>GET /finance/donations/payos-verify?orderId={orderCode}</c>
/// after PayOS redirects back to the success page.
/// </summary>
public class VerifyPayOSPaymentCommand : IRequest<bool>
{
    /// <summary>The orderCode returned by PayOS in the redirect URL query string.</summary>
    public required string OrderId { get; init; }
}
