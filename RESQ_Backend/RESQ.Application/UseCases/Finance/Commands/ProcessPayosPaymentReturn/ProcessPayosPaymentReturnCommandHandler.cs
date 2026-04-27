using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
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
        if (webhook?.Data == null)
        {
            _logger.LogWarning("PayOS webhook: empty webhook data.");
            return false;
        }

        var orderCode = webhook.Data.OrderCode.ToString();
        var paymentLinkId = webhook.Data.PaymentLinkId;

        _logger.LogInformation(
            "PayOS webhook received | OrderCode={OrderCode} PaymentLinkId={PaymentLinkId} Success={Success} Code={Code} Desc={Desc}.",
            orderCode,
            paymentLinkId,
            webhook.Success,
            webhook.Code,
            webhook.Desc);

        if (!webhook.Success || webhook.Code != "00")
        {
            _logger.LogWarning(
                "PayOS webhook indicates non-success payment | OrderCode={OrderCode} Code={Code} Desc={Desc}.",
                orderCode,
                webhook.Code,
                webhook.Desc);
            return true;
        }

        var donation = await _donationRepository.GetByOrderIdAsync(orderCode, cancellationToken);
        if (donation == null)
        {
            _logger.LogError("PayOS webhook: donation not found | OrderCode={OrderCode}.", orderCode);
            return false;
        }

        if (donation.Status == Status.Succeed)
        {
            _logger.LogInformation(
                "PayOS webhook idempotent skip: donation already succeeded | DonationId={DonationId} OrderCode={OrderCode}.",
                donation.Id,
                orderCode);
            return true;
        }

        var wasSucceededBefore = donation.Status == Status.Succeed;

        try
        {
            if (DateTime.TryParseExact(
                    webhook.Data.TransactionDateTime,
                    new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var paidAt))
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
                _logger.LogWarning(
                    "PayOS webhook success processing returned false | DonationId={DonationId} OrderCode={OrderCode}.",
                    donation.Id,
                    orderCode);
                return false;
            }

            if (!wasSucceededBefore && donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
            {
                await SendSuccessEmailAsync(donation, orderCode);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayOS webhook: error processing donation logic | OrderCode={OrderCode}.", orderCode);
            return false;
        }
    }

    private async Task SendSuccessEmailAsync(DonationModel donation, string orderCode)
    {
        try
        {
            _logger.LogInformation(
                "PayOS webhook: sending success email | DonationId={DonationId} OrderCode={OrderCode} Email={Email}.",
                donation.Id,
                orderCode,
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
                "PayOS webhook: success email sent | DonationId={DonationId} OrderCode={OrderCode}.",
                donation.Id,
                orderCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "PayOS webhook: success email failed after payment commit | DonationId={DonationId} OrderCode={OrderCode}.",
                donation.Id,
                orderCode);
        }
    }
}
