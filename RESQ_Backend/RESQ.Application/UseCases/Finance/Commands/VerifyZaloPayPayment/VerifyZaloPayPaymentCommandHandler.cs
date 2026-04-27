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
    private const int MaxQueryAttempts = 3;
    private static readonly TimeSpan QueryRetryDelay = TimeSpan.FromMilliseconds(700);

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

        var donation = await _donationRepository.GetByOrderIdAsync(appTransId, cancellationToken);
        if (donation == null)
        {
            _logger.LogWarning("VerifyZaloPay: donation not found for AppTransId {AppTransId}.", appTransId);
            return false;
        }

        if (donation.Status == Status.Succeed)
        {
            _logger.LogInformation(
                "VerifyZaloPay: donation {DonationId} already succeeded, idempotent skip | AppTransId={AppTransId}.",
                donation.Id,
                appTransId);
            return true;
        }

        var gatewayService = _paymentGatewayFactory.GetService(PaymentMethodCode.ZALOPAY);
        var queryResult = await QueryZaloPayWithRetryAsync(gatewayService, appTransId, cancellationToken);

        if (queryResult == null)
        {
            _logger.LogError("VerifyZaloPay: no usable response from ZaloPay query for {AppTransId}.", appTransId);
            return false;
        }

        if (queryResult.ReturnCode == 2)
        {
            var failed = await _donationPaymentProcessingService.TryProcessFailureAsync(
                donation.Id,
                $"[ZaloPay:ReturnCode={queryResult.ReturnCode},SubReturnCode={queryResult.SubReturnCode}][Source=QueryAPI]",
                cancellationToken);

            _logger.LogInformation(
                "VerifyZaloPay: marked donation as failed={MarkedFailed} | DonationId={DonationId} AppTransId={AppTransId} ReturnCode={ReturnCode} SubReturnCode={SubReturnCode}.",
                failed,
                donation.Id,
                appTransId,
                queryResult.ReturnCode,
                queryResult.SubReturnCode);

            return false;
        }

        if (queryResult.ReturnCode != 1)
        {
            _logger.LogWarning(
                "VerifyZaloPay: payment still pending/unknown, donation remains unchanged | DonationId={DonationId} AppTransId={AppTransId} ReturnCode={ReturnCode} SubReturnCode={SubReturnCode}.",
                donation.Id,
                appTransId,
                queryResult.ReturnCode,
                queryResult.SubReturnCode);
            return false;
        }

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
                _logger.LogWarning(
                    "VerifyZaloPay: success processing returned false | DonationId={DonationId} AppTransId={AppTransId}.",
                    donation.Id,
                    appTransId);
                return false;
            }

            if (!wasSucceededBefore && donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
            {
                await SendSuccessEmailAsync(donation, appTransId);
            }

            _logger.LogInformation(
                "VerifyZaloPay: donation {DonationId} successfully updated via query API | AppTransId={AppTransId} ZpTransId={ZpTransId}.",
                donation.Id,
                appTransId,
                queryResult.ZpTransId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VerifyZaloPay: error updating entities for AppTransId {AppTransId}.", appTransId);
            return false;
        }
    }

    private async Task<RESQ.Application.Common.Models.Finance.ZaloPay.ZaloPayQueryResponse?> QueryZaloPayWithRetryAsync(
        IPaymentGatewayService gatewayService,
        string appTransId,
        CancellationToken cancellationToken)
    {
        RESQ.Application.Common.Models.Finance.ZaloPay.ZaloPayQueryResponse? lastResult = null;

        for (var attempt = 1; attempt <= MaxQueryAttempts; attempt++)
        {
            lastResult = await gatewayService.QueryOrderAsync(appTransId, cancellationToken);
            if (lastResult == null)
            {
                _logger.LogWarning(
                    "VerifyZaloPay: query attempt {Attempt}/{MaxAttempts} returned null | AppTransId={AppTransId}.",
                    attempt,
                    MaxQueryAttempts,
                    appTransId);
            }
            else
            {
                _logger.LogInformation(
                    "VerifyZaloPay: query attempt {Attempt}/{MaxAttempts} | AppTransId={AppTransId} ReturnCode={ReturnCode} SubReturnCode={SubReturnCode} ReturnMessage={ReturnMessage} SubReturnMessage={SubReturnMessage}.",
                    attempt,
                    MaxQueryAttempts,
                    appTransId,
                    lastResult.ReturnCode,
                    lastResult.SubReturnCode,
                    lastResult.ReturnMessage,
                    lastResult.SubReturnMessage);

                if (lastResult.ReturnCode is 1 or 2)
                {
                    return lastResult;
                }
            }

            if (attempt < MaxQueryAttempts)
            {
                await Task.Delay(QueryRetryDelay, cancellationToken);
            }
        }

        return lastResult;
    }

    private async Task SendSuccessEmailAsync(RESQ.Domain.Entities.Finance.DonationModel donation, string appTransId)
    {
        try
        {
            _logger.LogInformation(
                "VerifyZaloPay: sending success email | DonationId={DonationId} AppTransId={AppTransId} Email={Email}.",
                donation.Id,
                appTransId,
                donation.Donor!.Email);

            await _emailService.SendDonationSuccessEmailAsync(
                donation.Donor.Email,
                donation.Donor.Name,
                donation.Amount?.Amount ?? 0,
                donation.FundCampaignName ?? "Campaign",
                donation.FundCampaignCode ?? "RESQ",
                donation.Id,
                CancellationToken.None);

            _logger.LogInformation(
                "VerifyZaloPay: success email sent | DonationId={DonationId} AppTransId={AppTransId}.",
                donation.Id,
                appTransId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "VerifyZaloPay: success email failed after payment commit | DonationId={DonationId} AppTransId={AppTransId}.",
                donation.Id,
                appTransId);
        }
    }
}
