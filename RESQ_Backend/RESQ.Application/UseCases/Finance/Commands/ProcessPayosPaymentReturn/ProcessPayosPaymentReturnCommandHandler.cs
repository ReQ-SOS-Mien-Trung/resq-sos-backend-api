using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Finance;
using System.Globalization;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessPayosPaymentReturn;

public class ProcessPayosPaymentReturnCommandHandler : IRequestHandler<ProcessPayosPaymentReturnCommand, bool>
{
    private readonly IDonationRepository _donationRepository;
    private readonly IDonationPaymentProcessingService _donationPaymentProcessingService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ProcessPayosPaymentReturnCommandHandler> _logger;

    public ProcessPayosPaymentReturnCommandHandler(
        IDonationRepository donationRepository,
        IDonationPaymentProcessingService donationPaymentProcessingService,
        IEmailService emailService,
        ILogger<ProcessPayosPaymentReturnCommandHandler> logger)
    {
        _donationRepository = donationRepository;
        _donationPaymentProcessingService = donationPaymentProcessingService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessPayosPaymentReturnCommand request, CancellationToken cancellationToken)
    {
        var webhook = request.WebhookData;
        if (webhook == null || webhook.Data == null)
        {
            _logger.LogWarning("Received empty webhook data.");
            return false;
        }

        var orderCodeStr = webhook.Data.OrderCode.ToString();
        var paymentLinkId = webhook.Data.PaymentLinkId;

        _logger.LogInformation("Processing webhook for OrderCode: {OrderCode}, PaymentLinkId: {PaymentLinkId}, Success: {Success}, Desc: {Desc}", 
            orderCodeStr, paymentLinkId, webhook.Success, webhook.Desc);

        // We only process successful payments via webhook reliably.
        if (!webhook.Success || webhook.Code != "00")
        {
            _logger.LogWarning("Webhook indicates non-success payment for OrderCode: {OrderCode}. Code: {Code}, Desc: {Desc}", 
                orderCodeStr, webhook.Code, webhook.Desc);
            return true; 
        }

        var donation = await _donationRepository.GetByOrderIdAsync(orderCodeStr, cancellationToken);
        
        if (donation == null)
        {
            _logger.LogError("Donation not found for OrderCode: {OrderCode}", orderCodeStr);
            return false;
        }

        // Idempotency guard - webhook may fire more than once
        if (donation.Status == Status.Succeed)
        {
            _logger.LogInformation("Donation {Id} already succeeded, ignoring duplicate webhook for OrderCode {OrderCode}.", donation.Id, orderCodeStr);
            return true;
        }

        var wasSucceededBefore = donation.Status == Status.Succeed;

        try 
        {
            if (DateTime.TryParseExact(webhook.Data.TransactionDateTime, new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var paidAt))
            {
                donation.PaidAt = paidAt.ToUniversalTime();
            }

            var processed = await _donationPaymentProcessingService.TryProcessSuccessAsync(
                donation.Id,
                $"[Bank:{webhook.Data.CounterAccountBankName}-{webhook.Data.CounterAccountNumber}]",
                donation.PaidAt ?? DateTime.UtcNow,
                paymentLinkId,
                preserveExistingTransactionId: true,
                cancellationToken);

            if (!processed)
            {
                return false;
            }

            if (!wasSucceededBefore && donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
            {
                await _emailService.SendDonationSuccessEmailAsync(
                    donation.Donor.Email, donation.Donor.Name, donation.Amount?.Amount ?? 0,
                    donation.FundCampaignName ?? "Chiến dịch", donation.FundCampaignCode ?? "RESQ",
                    donation.Id, CancellationToken.None
                );
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing donation logic for OrderCode {OrderCode}", orderCodeStr);
            // Return false or true depending on whether you want the gateway to retry. 
            // Usually internal logic errors shouldn't retry if they are permanent, but for transient issues return false.
            return false; 
        }
    }
}

