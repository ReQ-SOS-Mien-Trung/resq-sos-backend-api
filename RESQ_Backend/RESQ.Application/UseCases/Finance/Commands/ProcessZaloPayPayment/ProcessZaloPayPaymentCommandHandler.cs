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
                return false;
            }

            if (donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
            {
                await _emailService.SendDonationSuccessEmailAsync(
                    donation.Donor.Email, donation.Donor.Name, donation.Amount?.Amount ?? 0,
                    donation.FundCampaignName ?? "Campaign", donation.FundCampaignCode ?? "RESQ",
                    donation.Id, CancellationToken.None
                );
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ZaloPay callback logic.");
            return false;
        }
    }
}
