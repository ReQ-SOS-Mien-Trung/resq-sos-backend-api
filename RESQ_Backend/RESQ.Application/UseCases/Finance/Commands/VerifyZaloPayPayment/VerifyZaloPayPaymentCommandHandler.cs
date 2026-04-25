using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models.Finance.ZaloPay;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.VerifyZaloPayPayment;

public class VerifyZaloPayPaymentCommandHandler : IRequestHandler<VerifyZaloPayPaymentCommand, bool>
{
    private const int MaxQueryAttempts = 4;
    private static readonly TimeSpan QueryRetryDelay = TimeSpan.FromMilliseconds(750);

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

        _logger.LogInformation("VerifyZaloPay: starting verification for AppTransId={AppTransId}.", appTransId);

        var donation = await _donationRepository.GetByOrderIdAsync(appTransId, cancellationToken);
        if (donation == null)
        {
            _logger.LogWarning("VerifyZaloPay: donation not found for AppTransId={AppTransId}.", appTransId);
            return false;
        }

        if (donation.Status == Status.Succeed)
        {
            _logger.LogInformation("VerifyZaloPay: donation {DonationId} already succeeded, skipping.", donation.Id);
            return true;
        }

        var gatewayService = _paymentGatewayFactory.GetService(PaymentMethodCode.ZALOPAY);
        var queryResult = await QueryUntilFinalStatusAsync(gatewayService, appTransId, cancellationToken);

        if (queryResult == null)
        {
            if (IsSignedRedirectSuccess(request))
            {
                _logger.LogWarning(
                    "VerifyZaloPay: Query API returned null for AppTransId={AppTransId}, but signed redirect status indicates success. Processing success from signed redirect fallback.",
                    appTransId);
                return await ProcessSignedRedirectSuccessAsync(donation, cancellationToken);
            }

            _logger.LogError("VerifyZaloPay: null response from ZaloPay query for AppTransId={AppTransId}.", appTransId);
            return false;
        }

        _logger.LogInformation(
            "VerifyZaloPay: final query result for DonationId={DonationId}, AppTransId={AppTransId}: return_code={ReturnCode}, sub_return_code={SubReturnCode}, is_processing={IsProcessing}, zp_trans_id={ZpTransId}",
            donation.Id,
            appTransId,
            queryResult.ReturnCode,
            queryResult.SubReturnCode,
            queryResult.IsProcessing,
            queryResult.ZpTransId);

        if (queryResult.Amount > 0 && donation.Amount?.Amount != queryResult.Amount)
        {
            _logger.LogWarning(
                "VerifyZaloPay: amount mismatch for DonationId={DonationId}, AppTransId={AppTransId}. DonationAmount={DonationAmount}, ZaloPayAmount={ZaloPayAmount}.",
                donation.Id,
                appTransId,
                donation.Amount?.Amount,
                queryResult.Amount);
        }

        if (queryResult.ReturnCode == 1)
        {
            return await ProcessSuccessAsync(donation, queryResult, cancellationToken);
        }

        if (queryResult.ReturnCode == 2)
        {
            var processedFailure = await _donationPaymentProcessingService.TryProcessFailureAsync(
                donation.Id,
                $"[ZaloPay:Source=QueryAPI][ReturnCode={queryResult.ReturnCode}][SubReturnCode={queryResult.SubReturnCode}]",
                cancellationToken);

            _logger.LogWarning(
                "VerifyZaloPay: payment failed for DonationId={DonationId}, AppTransId={AppTransId}. FailureProcessed={FailureProcessed}.",
                donation.Id,
                appTransId,
                processedFailure);
            return false;
        }

        _logger.LogWarning(
            "VerifyZaloPay: payment is still pending for DonationId={DonationId}, AppTransId={AppTransId} after {Attempts} attempts.",
            donation.Id,
            appTransId,
            MaxQueryAttempts);

        if (IsSignedRedirectSuccess(request))
        {
            _logger.LogWarning(
                "VerifyZaloPay: Query API is still pending for AppTransId={AppTransId}, but signed redirect status indicates success. Processing success from signed redirect fallback.",
                appTransId);
            return await ProcessSignedRedirectSuccessAsync(donation, cancellationToken);
        }

        return false;
    }

    private async Task<bool> ProcessSuccessAsync(
        RESQ.Domain.Entities.Finance.DonationModel donation,
        ZaloPayQueryResponse queryResult,
        CancellationToken cancellationToken)
    {
        var wasSucceededBefore = donation.Status == Status.Succeed;
        var paidAtUtc = queryResult.ServerTime > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(queryResult.ServerTime).UtcDateTime
            : DateTime.UtcNow;

        try
        {
            var processed = await _donationPaymentProcessingService.TryProcessSuccessAsync(
                donation.Id,
                $"[ZaloPay:ZpTransId={queryResult.ZpTransId}][Source=QueryAPI]",
                paidAtUtc,
                queryResult.ZpTransId.ToString(),
                preserveExistingTransactionId: false,
                cancellationToken);

            if (!processed)
            {
                _logger.LogWarning(
                    "VerifyZaloPay: TryProcessSuccessAsync returned false for DonationId={DonationId}, AppTransId={AppTransId}.",
                    donation.Id,
                    donation.OrderId);
                return false;
            }

            if (!wasSucceededBefore && donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
            {
                _ = _emailService.SendDonationSuccessEmailAsync(
                    donation.Donor.Email,
                    donation.Donor.Name,
                    donation.Amount?.Amount ?? 0,
                    donation.FundCampaignName ?? "Campaign",
                    donation.FundCampaignCode ?? "RESQ",
                    donation.Id,
                    cancellationToken);
            }

            _logger.LogInformation("VerifyZaloPay: donation {DonationId} successfully updated via query API.", donation.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VerifyZaloPay: error updating entities for AppTransId={AppTransId}.", donation.OrderId);
            return false;
        }
    }

    private async Task<bool> ProcessSignedRedirectSuccessAsync(
        RESQ.Domain.Entities.Finance.DonationModel donation,
        CancellationToken cancellationToken)
    {
        try
        {
            var processed = await _donationPaymentProcessingService.TryProcessSuccessAsync(
                donation.Id,
                "[ZaloPay:Source=SignedRedirect][Status=1]",
                DateTime.UtcNow,
                donation.OrderId,
                preserveExistingTransactionId: true,
                cancellationToken);

            if (!processed)
            {
                _logger.LogWarning(
                    "VerifyZaloPay: signed redirect fallback TryProcessSuccessAsync returned false for DonationId={DonationId}, AppTransId={AppTransId}.",
                    donation.Id,
                    donation.OrderId);
                return false;
            }

            if (donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
            {
                _ = _emailService.SendDonationSuccessEmailAsync(
                    donation.Donor.Email,
                    donation.Donor.Name,
                    donation.Amount?.Amount ?? 0,
                    donation.FundCampaignName ?? "Campaign",
                    donation.FundCampaignCode ?? "RESQ",
                    donation.Id,
                    cancellationToken);
            }

            _logger.LogInformation("VerifyZaloPay: donation {DonationId} successfully updated via signed redirect fallback.", donation.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VerifyZaloPay: error updating entities from signed redirect fallback for AppTransId={AppTransId}.", donation.OrderId);
            return false;
        }
    }

    private static bool IsSignedRedirectSuccess(VerifyZaloPayPaymentCommand request)
        => request.HasValidRedirectChecksum && request.SignedRedirectStatus == "1";

    private async Task<ZaloPayQueryResponse?> QueryUntilFinalStatusAsync(
        IPaymentGatewayService gatewayService,
        string appTransId,
        CancellationToken cancellationToken)
    {
        ZaloPayQueryResponse? lastResult = null;

        for (var attempt = 1; attempt <= MaxQueryAttempts; attempt++)
        {
            lastResult = await gatewayService.QueryOrderAsync(appTransId, cancellationToken);

            if (lastResult == null)
            {
                _logger.LogWarning("VerifyZaloPay: attempt {Attempt}/{MaxAttempts} returned null for AppTransId={AppTransId}.", attempt, MaxQueryAttempts, appTransId);
                return null;
            }

            _logger.LogInformation(
                "VerifyZaloPay: attempt {Attempt}/{MaxAttempts} for AppTransId={AppTransId} returned return_code={ReturnCode}, sub_return_code={SubReturnCode}, is_processing={IsProcessing}.",
                attempt,
                MaxQueryAttempts,
                appTransId,
                lastResult.ReturnCode,
                lastResult.SubReturnCode,
                lastResult.IsProcessing);

            if (lastResult.ReturnCode is 1 or 2 || !lastResult.IsProcessing)
            {
                return lastResult;
            }

            if (attempt < MaxQueryAttempts)
            {
                await Task.Delay(QueryRetryDelay, cancellationToken);
            }
        }

        return lastResult;
    }
}
