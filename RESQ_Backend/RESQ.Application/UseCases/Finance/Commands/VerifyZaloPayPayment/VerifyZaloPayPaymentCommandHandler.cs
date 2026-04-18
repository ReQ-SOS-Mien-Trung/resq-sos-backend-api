using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.VerifyZaloPayPayment;

/// <summary>
/// Fallback handler: when ZaloPay does not fire the callback (e.g. sandbox or network issues),
/// the frontend calls this after the redirect so the backend can query ZaloPay directly
/// and update the donation/campaign entities.
/// </summary>
public class VerifyZaloPayPaymentCommandHandler : IRequestHandler<VerifyZaloPayPaymentCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDonationRepository _donationRepository;
    private readonly IFundCampaignRepository _campaignRepository;
    private readonly IFundTransactionRepository _transactionRepository;
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;
    private readonly IEmailService _emailService;
    private readonly ILogger<VerifyZaloPayPaymentCommandHandler> _logger;

    public VerifyZaloPayPaymentCommandHandler(
        IUnitOfWork unitOfWork,
        IDonationRepository donationRepository,
        IFundCampaignRepository campaignRepository,
        IFundTransactionRepository transactionRepository,
        IPaymentGatewayFactory paymentGatewayFactory,
        IEmailService emailService,
        ILogger<VerifyZaloPayPaymentCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _donationRepository = donationRepository;
        _campaignRepository = campaignRepository;
        _transactionRepository = transactionRepository;
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
        try
        {
            donation.UpdatePaymentStatus(Status.Succeed);
            donation.TransactionId = queryResult.ZpTransId.ToString();
            donation.PaymentAuditInfo = $"[ZaloPay:ZpTransId={queryResult.ZpTransId}][Source=QueryAPI]";
            donation.PaidAt = DateTimeOffset.FromUnixTimeMilliseconds(queryResult.ServerTime).UtcDateTime;

            await _donationRepository.UpdateAsync(donation, cancellationToken);

            // 4. Update fund campaign
            if (donation.FundCampaignId.HasValue)
            {
                var campaign = await _campaignRepository.GetByIdAsync(donation.FundCampaignId.Value, cancellationToken);
                if (campaign != null && !campaign.IsDeleted)
                {
                    campaign.ReceiveDonation(donation.Amount?.Amount ?? 0);
                    await _campaignRepository.UpdateAsync(campaign, cancellationToken);

                    var transaction = new FundTransactionModel
                    {
                        FundCampaignId = donation.FundCampaignId,
                        Type = TransactionType.Donation,
                        Direction = TransactionDirection.In,
                        Amount = donation.Amount?.Amount,
                        ReferenceType = TransactionReferenceType.Donation,
                        ReferenceId = donation.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _transactionRepository.CreateAsync(transaction, cancellationToken);
                }
            }

            await _unitOfWork.SaveAsync();

            // 5. Send confirmation email (fire-and-forget)
            if (donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
            {
                _ = _emailService.SendDonationSuccessEmailAsync(
                    donation.Donor.Email, donation.Donor.Name, donation.Amount?.Amount ?? 0,
                    donation.FundCampaignName ?? "Campaign", donation.FundCampaignCode ?? "RESQ",
                    donation.Id, cancellationToken
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
