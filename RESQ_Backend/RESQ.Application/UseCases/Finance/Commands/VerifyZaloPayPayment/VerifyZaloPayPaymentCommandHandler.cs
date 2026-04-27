using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.VerifyZaloPayPayment;

/// <summary>
/// Fallback handler: when ZaloPay does not fire the callback (e.g. sandbox or network issues),
/// the frontend calls this after the redirect so the backend can query ZaloPay directly
/// and update the donation/campaign entities.
/// </summary>
public class VerifyZaloPayPaymentCommandHandler : IRequestHandler<VerifyZaloPayPaymentCommand, bool>
{
    private readonly IDonationRepository _donationRepository;
    private readonly IDonationPaymentProcessingService _donationPaymentProcessingService;
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;
    private readonly IEmailService _emailService;
    private readonly ILogger<VerifyZaloPayPaymentCommandHandler> _logger;

    public VerifyZaloPayPaymentCommandHandler(
        IDonationRepository donationRepository,
        IDonationPaymentProcessingService donationPaymentProcessingService,
        IPaymentGatewayFactory paymentGatewayFactory,
        IEmailService emailService,
        ILogger<VerifyZaloPayPaymentCommandHandler> logger)
    {
        _donationRepository = donationRepository;
        _donationPaymentProcessingService = donationPaymentProcessingService;
        _paymentGatewayFactory = paymentGatewayFactory;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<bool> Handle(VerifyZaloPayPaymentCommand request, CancellationToken cancellationToken)
    {
        var appTransId = request.AppTransId;
        if (string.IsNullOrWhiteSpace(appTransId))
        {
            _logger.LogWarning("VerifyZaloPay: empty AppTransId.");
            return false;
        }

        // 1. Look up the donation first - if already succeeded, nothing to do
        var donation = await _donationRepository.GetByOrderIdAsync(appTransId, cancellationToken);
        if (donation == null)
        {
            _logger.LogWarning("VerifyZaloPay: donation not found for AppTransId {AppTransId}.", appTransId);
            return false;
        }

        if (donation.Status == Status.Succeed)
        {
            _logger.LogInformation("VerifyZaloPay: donation {Id} already succeeded, skipping.", donation.Id);
            return true;
        }

        // 2. Query ZaloPay Order Query API
        var gatewayService = _paymentGatewayFactory.GetService(PaymentMethodCode.ZALOPAY);
        var queryResult = await gatewayService.QueryOrderAsync(appTransId, cancellationToken);

        if (queryResult == null)
        {
            _logger.LogError("VerifyZaloPay: null response from ZaloPay query for {AppTransId}.", appTransId);
            return false;
        }

        _logger.LogInformation("VerifyZaloPay: ZaloPay query return_code={Code} for {AppTransId}.",
            queryResult.ReturnCode, appTransId);

        // return_code: 1 = paid, 2 = rejected/failed, 3 = processing/pending
        if (queryResult.ReturnCode != 1)
        {
            _logger.LogWarning("VerifyZaloPay: payment not confirmed (return_code={Code}) for {AppTransId}.",
                queryResult.ReturnCode, appTransId);
            return false;
        }

        // 3. Update donation
        var wasSucceededBefore = donation.Status == Status.Succeed;

        try
        {
            var processed = await _donationPaymentProcessingService.TryProcessSuccessAsync(
                donation.Id,
                $"[ZaloPay:ZpTransId={queryResult.ZpTransId}][Source=QueryAPI]",
                DateTimeOffset.FromUnixTimeMilliseconds(queryResult.ServerTime).UtcDateTime,
                queryResult.ZpTransId.ToString(),
                preserveExistingTransactionId: false,
                cancellationToken);

            if (!processed)
            {
                return false;
            }

            // 5. Send confirmation email
            if (!wasSucceededBefore && donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
            {
                await _emailService.SendDonationSuccessEmailAsync(
                    donation.Donor.Email, donation.Donor.Name, donation.Amount?.Amount ?? 0,
                    donation.FundCampaignName ?? "Campaign", donation.FundCampaignCode ?? "RESQ",
                    donation.Id, CancellationToken.None
                );
            }

            _logger.LogInformation("VerifyZaloPay: donation {Id} successfully updated via query API.", donation.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VerifyZaloPay: error updating entities for AppTransId {AppTransId}.", appTransId);
            return false;
        }
    }
}
