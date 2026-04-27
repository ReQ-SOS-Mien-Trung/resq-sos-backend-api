using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.VerifyPayOSPayment;

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
        var donation = await _donationRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (donation == null)
        {
            _logger.LogWarning("VerifyPayOS: donation not found | OrderId={OrderId}.", orderId);
            return false;
        }

        if (donation.Status == Status.Succeed)
        {
            _logger.LogInformation("VerifyPayOS: donation already succeeded | DonationId={DonationId} OrderId={OrderId}.", donation.Id, orderId);
            return true;
        }

        if (string.IsNullOrEmpty(donation.TransactionId))
        {
            _logger.LogWarning("VerifyPayOS: donation has no TransactionId/paymentLinkId | DonationId={DonationId} OrderId={OrderId}.", donation.Id, orderId);
            return false;
        }

        var gatewayService = _paymentGatewayFactory.GetService(PaymentMethodCode.PAYOS);
        var queryResult = await gatewayService.QueryPaymentLinkAsync(donation.TransactionId, cancellationToken);

        if (queryResult == null)
        {
            _logger.LogError(
                "VerifyPayOS: null response from PayOS query | DonationId={DonationId} PaymentLinkId={PaymentLinkId}.",
                donation.Id,
                donation.TransactionId);
            return false;
        }

        _logger.LogInformation(
            "VerifyPayOS: PayOS query result | DonationId={DonationId} Status={Status} ReturnCode={ReturnCode}.",
            donation.Id,
            queryResult.ReturnMessage,
            queryResult.ReturnCode);

        if (queryResult.ReturnCode != 1)
        {
            _logger.LogWarning(
                "VerifyPayOS: payment not confirmed | DonationId={DonationId} ReturnCode={ReturnCode}.",
                donation.Id,
                queryResult.ReturnCode);
            return false;
        }

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
                _logger.LogWarning("VerifyPayOS: success processing returned false | DonationId={DonationId}.", donation.Id);
                return false;
            }

            if (!wasSucceededBefore && donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
            {
                await SendSuccessEmailAsync(donation, orderId);
            }

            _logger.LogInformation("VerifyPayOS: donation successfully updated via PayOS Query API | DonationId={DonationId}.", donation.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VerifyPayOS: error updating entities | DonationId={DonationId}.", donation.Id);
            return false;
        }
    }

    private async Task SendSuccessEmailAsync(DonationModel donation, string orderId)
    {
        try
        {
            _logger.LogInformation(
                "VerifyPayOS: sending success email | DonationId={DonationId} OrderId={OrderId} Email={Email}.",
                donation.Id,
                orderId,
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
                "VerifyPayOS: success email sent | DonationId={DonationId} OrderId={OrderId}.",
                donation.Id,
                orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "VerifyPayOS: success email failed after payment commit | DonationId={DonationId} OrderId={OrderId}.",
                donation.Id,
                orderId);
        }
    }
}
