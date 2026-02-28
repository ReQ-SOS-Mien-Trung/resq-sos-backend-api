using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using System.Globalization;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessPaymentReturn;

public class ProcessPaymentReturnCommandHandler : IRequestHandler<ProcessPaymentReturnCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDonationRepository _donationRepository;
    private readonly IFundCampaignRepository _campaignRepository;
    private readonly IFundTransactionRepository _transactionRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<ProcessPaymentReturnCommandHandler> _logger;

    public ProcessPaymentReturnCommandHandler(
        IUnitOfWork unitOfWork,
        IDonationRepository donationRepository,
        IFundCampaignRepository campaignRepository,
        IFundTransactionRepository transactionRepository,
        IEmailService emailService,
        ILogger<ProcessPaymentReturnCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _donationRepository = donationRepository;
        _campaignRepository = campaignRepository;
        _transactionRepository = transactionRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessPaymentReturnCommand request, CancellationToken cancellationToken)
    {
        var webhook = request.WebhookData;
        if (webhook == null || webhook.Data == null)
        {
            _logger.LogWarning("Received empty webhook data from PayOS.");
            return false;
        }

        var orderCodeStr = webhook.Data.OrderCode.ToString();
        _logger.LogInformation("Processing payment return for OrderCode: {OrderCode}, Success: {Success}", orderCodeStr, webhook.Success);

        // 1. Find Donation by Order Code
        var donation = await _donationRepository.GetByPayosOrderIdAsync(orderCodeStr, cancellationToken);

        if (donation == null)
        {
            _logger.LogError("Donation not found for OrderCode: {OrderCode}", orderCodeStr);
            throw new NotFoundException($"Không tìm thấy đơn ủng hộ với mã: {orderCodeStr}");
        }

        // 2. Determine Status
        var newStatus = PayOSStatus.Failed;

        if (webhook.Success || webhook.Code == "00")
        {
            newStatus = PayOSStatus.Succeed; // Note: Ensure Enum is 'Succeed'
        }

        // 3. Update Donation
        // Only process if status changed (e.g. Pending -> Succeed)
        if (donation.PayosStatus != newStatus)
        {
            donation.PayosStatus = newStatus;
            donation.PayosTransactionId = webhook.Data.PaymentLinkId;

            if (newStatus == PayOSStatus.Succeed)
            {
                if (DateTime.TryParseExact(
                        webhook.Data.TransactionDateTime,
                        "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var paidAt))
                {
                    donation.PaidAt = paidAt.ToUniversalTime();
                }
            }

            await _donationRepository.UpdateAsync(donation, cancellationToken);
            
            // 4. Business Logic for Successful Donation
            if (newStatus == PayOSStatus.Succeed)
            {
                if (!donation.FundCampaignId.HasValue)
                {
                    _logger.LogError("Donation {Id} is missing FundCampaignId", donation.Id);
                    throw new InvalidOperationException($"Đơn ủng hộ {donation.Id} không có thông tin chiến dịch gây quỹ");
                }

                // 4.1 Update Campaign Total
                var campaign = await _campaignRepository.GetByIdAsync(donation.FundCampaignId.Value, cancellationToken);
                if (campaign != null)
                {
                    var donationAmount = donation.Amount?.Amount ?? 0;
                    campaign.TotalAmount = (campaign.TotalAmount ?? 0) + donationAmount;
                    
                    await _campaignRepository.UpdateAsync(campaign, cancellationToken);
                }

                // 4.2 Create Fund Transaction
                var transaction = new FundTransactionModel
                {
                    FundCampaignId = donation.FundCampaignId,
                    Type = TransactionType.Donation,
                    Direction = "in",
                    Amount = donation.Amount?.Amount,
                    ReferenceType = TransactionReferenceType.Donation,
                    ReferenceId = donation.Id,
                    CreatedAt = DateTime.UtcNow,
                }
            ;
                await _transactionRepository.CreateAsync(transaction, cancellationToken);

                // 4.3 Send Confirmation Email
                // We do this inside the try block but ideally email failures shouldn't rollback DB
                // Since this runs within a transaction scope of SaveAsync potentially, 
                // we'll trigger email *after* DB commit or swallow exceptions carefully.
            }

            // 5. Save all changes atomically
            var result = await _unitOfWork.SaveAsync();
            
            if (result > 0 && newStatus == PayOSStatus.Succeed)
            {
                // Send email AFTER successful DB commit
                if (donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
                {
                    try 
                    {
                        var campaignName = donation.FundCampaignName ?? "Chiến dịch";
                        var campaignCode = donation.FundCampaignCode ?? "Unknown";

                        await _emailService.SendDonationSuccessEmailAsync(
                            donation.Donor.Email,
                            donation.Donor.Name,
                            donation.Amount?.Amount ?? 0,
                            campaignName,
                            campaignCode,
                            donation.Id,
                            cancellationToken
                        );
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send confirmation email for donation {Id}", donation.Id);
                    }
                }
            }
            
            _logger.LogInformation("Donation {Id} processed with status {Status}.", donation.Id, newStatus);
        }

        return true;
    }
}
