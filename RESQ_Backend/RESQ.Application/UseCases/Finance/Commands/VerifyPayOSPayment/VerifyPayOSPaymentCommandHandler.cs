using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.VerifyPayOSPayment;

/// <summary>
/// Fallback handler: when the PayOS webhook does not reliably update the DB
/// (e.g. webhook URL configured in PayOS dashboard points to a different server),
/// the frontend calls <c>GET /finance/donations/payos-verify?orderId={orderCode}</c>
/// immediately after PayOS redirects back to the success page.
/// </summary>
public class VerifyPayOSPaymentCommandHandler : IRequestHandler<VerifyPayOSPaymentCommand, bool>
{
    private readonly IDonationRepository _donationRepository;
    private readonly IDonationPaymentProcessingService _donationPaymentProcessingService;
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;
    private readonly IEmailService _emailService;
    private readonly ILogger<VerifyPayOSPaymentCommandHandler> _logger;

    public VerifyPayOSPaymentCommandHandler(
        IDonationRepository donationRepository,
        IDonationPaymentProcessingService donationPaymentProcessingService,
        IPaymentGatewayFactory paymentGatewayFactory,
        IEmailService emailService,
        ILogger<VerifyPayOSPaymentCommandHandler> logger)
    {
        _donationRepository = donationRepository;
        _donationPaymentProcessingService = donationPaymentProcessingService;
        _paymentGatewayFactory = paymentGatewayFactory;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<bool> Handle(VerifyPayOSPaymentCommand request, CancellationToken cancellationToken)
    {
        var orderId = request.OrderId;

        // 1. Look up the donation - if already succeeded, nothing to do
        var donation = await _donationRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (donation == null)
        {
            _logger.LogWarning("VerifyPayOS: donation not found for OrderId {OrderId}.", orderId);
            return false;
        }

        if (donation.Status == Status.Succeed)
        {
            _logger.LogInformation("VerifyPayOS: donation {Id} already succeeded, skipping.", donation.Id);
            return true;
        }

        // 2. Require a stored TransactionId (= PayOS paymentLinkId set during CreateDonation)
        if (string.IsNullOrEmpty(donation.TransactionId))
        {
            _logger.LogWarning("VerifyPayOS: donation {Id} has no TransactionId (paymentLinkId) stored — cannot query PayOS.", donation.Id);
            return false;
        }

        // 3. Query PayOS order status via the payment link API
        var gatewayService = _paymentGatewayFactory.GetService(PaymentMethodCode.PAYOS);
        var queryResult = await gatewayService.QueryPaymentLinkAsync(donation.TransactionId, cancellationToken);

        if (queryResult == null)
        {
            _logger.LogError("VerifyPayOS: null response from PayOS query for donation {Id} / paymentLinkId {LinkId}.",
                donation.Id, donation.TransactionId);
            return false;
        }

        _logger.LogInformation("VerifyPayOS: PayOS status={Status} (ReturnCode={Code}) for donation {Id}.",
            queryResult.ReturnMessage, queryResult.ReturnCode, donation.Id);

        // ReturnCode: 1 = PAID, 2 = failed/cancelled, 3 = processing/pending
        if (queryResult.ReturnCode != 1)
        {
            _logger.LogWarning("VerifyPayOS: payment not confirmed (ReturnCode={Code}) for donation {Id}.",
                queryResult.ReturnCode, donation.Id);
            return false;
        }

        // 4. Update donation - deliberately keep existing TransactionId (paymentLinkId)
        var wasSucceededBefore = donation.Status == Status.Succeed;

        try
        {
            var processed = await _donationPaymentProcessingService.TryProcessSuccessAsync(
                donation.Id,
                $"[PayOS:status=PAID][Source=QueryAPI][LinkId={donation.TransactionId}]",
                DateTime.UtcNow,
                donation.TransactionId,
                preserveExistingTransactionId: true,
                cancellationToken);

            if (!processed)
            {
                return false;
            }

            // 6. Send confirmation email (fire-and-forget)
            if (!wasSucceededBefore && donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
            {
                _ = _emailService.SendDonationSuccessEmailAsync(
                    donation.Donor.Email, donation.Donor.Name, donation.Amount?.Amount ?? 0,
                    donation.FundCampaignName ?? "Chiến dịch", donation.FundCampaignCode ?? "RESQ",
                    donation.Id, cancellationToken
                );
            }

            _logger.LogInformation("VerifyPayOS: donation {Id} successfully updated via PayOS Query API.", donation.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VerifyPayOS: error updating entities for donation {Id}.", donation.Id);
            return false;
        }
    }
}
