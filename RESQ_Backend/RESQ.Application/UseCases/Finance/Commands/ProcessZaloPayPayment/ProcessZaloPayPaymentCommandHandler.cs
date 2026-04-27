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

        ZaloPayCallbackData? dataJson;
        try
        {
            dataJson = JsonSerializer.Deserialize<ZaloPayCallbackData>(cbdata.Data);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize ZaloPay callback data.");
            return false;
        }

        if (dataJson == null)
        {
            _logger.LogWarning("Deserialized ZaloPay data is null.");
            return false;
        }

        _logger.LogInformation(
            "ZaloPay callback received | AppTransId={AppTransId} ZpTransId={ZpTransId} Amount={Amount} ServerTime={ServerTime}.",
            dataJson.AppTransId,
            dataJson.ZpTransId,
            dataJson.Amount,
            dataJson.ServerTime);

        var donation = await _donationRepository.GetByOrderIdAsync(dataJson.AppTransId, cancellationToken);
        if (donation == null)
        {
            _logger.LogWarning("Donation not found for AppTransId: {AppTransId}", dataJson.AppTransId);
            return true; // Return true so ZaloPay stops retrying an unknown ID
        }

        try
        {
            if (donation.Status == Status.Succeed)
            {
                _logger.LogInformation(
                    "ZaloPay callback idempotent skip: donation already succeeded | DonationId={DonationId} AppTransId={AppTransId}.",
                    donation.Id,
                    dataJson.AppTransId);
                return true;
            }

            var processed = await _donationPaymentProcessingService.TryProcessSuccessAsync(
                donation.Id,
                $"[ZaloPay:ZpTransId={dataJson.ZpTransId}]",
                DateTimeOffset.FromUnixTimeMilliseconds(dataJson.ServerTime).UtcDateTime,
                dataJson.ZpTransId.ToString(),
                preserveExistingTransactionId: false,
                cancellationToken);

            if (!processed)
            {
                _logger.LogWarning(
                    "ZaloPay callback success processing returned false | DonationId={DonationId} AppTransId={AppTransId}.",
                    donation.Id,
                    dataJson.AppTransId);
                return false;
            }

            if (donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
            {
                await SendSuccessEmailAsync(donation, dataJson.AppTransId);
            }

            _logger.LogInformation(
                "ZaloPay callback processed successfully | DonationId={DonationId} AppTransId={AppTransId} ZpTransId={ZpTransId}.",
                donation.Id,
                dataJson.AppTransId,
                dataJson.ZpTransId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ZaloPay callback logic.");
            return false;
        }
    }

    private async Task SendSuccessEmailAsync(RESQ.Domain.Entities.Finance.DonationModel donation, string appTransId)
    {
        try
        {
            _logger.LogInformation(
                "ZaloPay callback: sending success email | DonationId={DonationId} AppTransId={AppTransId} Email={Email}.",
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
                "ZaloPay callback: success email sent | DonationId={DonationId} AppTransId={AppTransId}.",
                donation.Id,
                appTransId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ZaloPay callback: success email failed after payment commit | DonationId={DonationId} AppTransId={AppTransId}.",
                donation.Id,
                appTransId);
        }
    }
}
