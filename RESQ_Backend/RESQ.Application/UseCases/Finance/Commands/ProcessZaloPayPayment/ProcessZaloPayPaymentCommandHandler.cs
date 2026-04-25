using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models.Finance.ZaloPay;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Finance;
using System.Text.Json;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessZaloPayPayment;

public class ProcessZaloPayPaymentCommandHandler : IRequestHandler<ProcessZaloPayPaymentCommand, bool>
{
    private readonly IDonationRepository _donationRepository;
    private readonly IDonationPaymentProcessingService _donationPaymentProcessingService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ProcessZaloPayPaymentCommandHandler> _logger;

    public ProcessZaloPayPaymentCommandHandler(
        IDonationRepository donationRepository,
        IDonationPaymentProcessingService donationPaymentProcessingService,
        IEmailService emailService,
        ILogger<ProcessZaloPayPaymentCommandHandler> logger)
    {
        _donationRepository = donationRepository;
        _donationPaymentProcessingService = donationPaymentProcessingService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessZaloPayPaymentCommand request, CancellationToken cancellationToken)
    {
        var cbdata = request.CallbackData;
        if (cbdata.Type != 1)
        {
            _logger.LogWarning("ProcessZaloPayPayment: unsupported callback type {CallbackType}.", cbdata.Type);
            return false;
        }

        ZaloPayCallbackData? dataJson;
        try
        {
            dataJson = JsonSerializer.Deserialize<ZaloPayCallbackData>(cbdata.Data);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "ProcessZaloPayPayment: failed to deserialize callback data.");
            return false;
        }

        if (dataJson == null)
        {
            _logger.LogWarning("ProcessZaloPayPayment: deserialized ZaloPay callback data is null.");
            return false;
        }

        _logger.LogInformation(
            "ProcessZaloPayPayment: received callback for AppTransId={AppTransId}, ZpTransId={ZpTransId}, Amount={Amount}, ServerTime={ServerTime}.",
            dataJson.AppTransId,
            dataJson.ZpTransId,
            dataJson.Amount,
            dataJson.ServerTime);

        var donation = await _donationRepository.GetByOrderIdAsync(dataJson.AppTransId, cancellationToken);
        if (donation == null)
        {
            _logger.LogWarning("ProcessZaloPayPayment: donation not found for AppTransId={AppTransId}. Returning success so ZaloPay stops retrying.", dataJson.AppTransId);
            return true;
        }

        if (donation.Amount?.Amount != dataJson.Amount)
        {
            _logger.LogWarning(
                "ProcessZaloPayPayment: amount mismatch for DonationId={DonationId}, AppTransId={AppTransId}. DonationAmount={DonationAmount}, CallbackAmount={CallbackAmount}.",
                donation.Id,
                dataJson.AppTransId,
                donation.Amount?.Amount,
                dataJson.Amount);
        }

        try
        {
            if (donation.Status == Status.Succeed)
            {
                _logger.LogInformation("ProcessZaloPayPayment: donation {DonationId} already succeeded, ignoring duplicate callback.", donation.Id);
                return true;
            }

            var paidAtUtc = dataJson.ServerTime > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(dataJson.ServerTime).UtcDateTime
                : DateTime.UtcNow;

            var processed = await _donationPaymentProcessingService.TryProcessSuccessAsync(
                donation.Id,
                $"[ZaloPay:ZpTransId={dataJson.ZpTransId}][Source=Callback]",
                paidAtUtc,
                dataJson.ZpTransId.ToString(),
                preserveExistingTransactionId: false,
                cancellationToken);

            if (!processed)
            {
                _logger.LogWarning("ProcessZaloPayPayment: TryProcessSuccessAsync returned false for DonationId={DonationId}, AppTransId={AppTransId}.", donation.Id, dataJson.AppTransId);
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

            _logger.LogInformation("ProcessZaloPayPayment: donation {DonationId} updated to Succeed from callback for AppTransId={AppTransId}.", donation.Id, dataJson.AppTransId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessZaloPayPayment: error processing callback for AppTransId={AppTransId}.", dataJson.AppTransId);
            return false;
        }
    }
}
