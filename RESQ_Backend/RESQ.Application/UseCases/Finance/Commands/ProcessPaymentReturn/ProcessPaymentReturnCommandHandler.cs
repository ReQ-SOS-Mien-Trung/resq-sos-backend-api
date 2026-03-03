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
            _logger.LogWarning("Received empty webhook data.");
            return false;
        }

        // NOTE: Signature verification is now handled in the Controller using the raw request body.
        // We assume the data here is authentic.

        var orderCodeStr = webhook.Data.OrderCode.ToString();
        _logger.LogInformation("Processing payment return for OrderCode: {OrderCode}, Success: {Success}", orderCodeStr, webhook.Success);

        // 1. Find Donation
        var donation = await _donationRepository.GetByPayosOrderIdAsync(orderCodeStr, cancellationToken);

        if (donation == null)
        {
            _logger.LogError("Donation not found for OrderCode: {OrderCode}", orderCodeStr);
            throw new NotFoundException($"Không tìm thấy đơn ủng hộ với mã: {orderCodeStr}");
        }

        // 2. Idempotency Check
        if (donation.PayosStatus == PayOSStatus.Succeed)
        {
            _logger.LogInformation("Donation {Id} is already processed (Success). Ignoring webhook.", donation.Id);
            return true;
        }

        // 3. Determine Status
        var newStatus = PayOSStatus.Failed;

        // "00" is success code for PayOS
        if (webhook.Success || webhook.Code == "00")
        {
            newStatus = PayOSStatus.Succeed;
        }

        // 4. Update Donation Status
        donation.PayosStatus = newStatus;
        donation.PayosTransactionId = webhook.Data.PaymentLinkId;

        if (newStatus == PayOSStatus.Succeed)
        {
            // Set paid time
            if (DateTime.TryParseExact(
                    webhook.Data.TransactionDateTime,
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var paidAt))
            {
                donation.PaidAt = paidAt.ToUniversalTime();
            }
            else
            {
                donation.PaidAt = DateTime.UtcNow;
            }
        }

        await _donationRepository.UpdateAsync(donation, cancellationToken);
        
        // 5. Handle Business Logic for Success
        if (newStatus == PayOSStatus.Succeed)
        {
            if (!donation.FundCampaignId.HasValue)
            {
                _logger.LogError("Donation {Id} is missing FundCampaignId", donation.Id);
            }
            else
            {
                // 5.1 Update Campaign Total
                var campaign = await _campaignRepository.GetByIdAsync(donation.FundCampaignId.Value, cancellationToken);
                if (campaign != null)
                {
                    var donationAmount = donation.Amount?.Amount ?? 0;
                    
                    // Use Domain Method to update TotalAmount securely
                    campaign.ReceiveDonation(donationAmount);
                    
                    await _campaignRepository.UpdateAsync(campaign, cancellationToken);
                }

                // 5.2 Create Fund Transaction
                var transaction = new FundTransactionModel
                {
                    FundCampaignId = donation.FundCampaignId,
                    Type = TransactionType.Donation,
                    Direction = "in",
                    Amount = donation.Amount?.Amount,
                    ReferenceType = TransactionReferenceType.Donation,
                    ReferenceId = donation.Id,
                    CreatedBy = null, // System / Public user
                    CreatedAt = DateTime.UtcNow,
                };
                await _transactionRepository.CreateAsync(transaction, cancellationToken);
            }
        }

        // 6. Save Transaction
        var result = await _unitOfWork.SaveAsync();
        
        // 7. Send Email if Success
        if (result > 0 && newStatus == PayOSStatus.Succeed)
        {
            if (donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
            {
                try 
                {
                    var campaignName = donation.FundCampaignName ?? "Chiến dịch cứu trợ";
                    var campaignCode = donation.FundCampaignCode ?? "RESQ";

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
        
        return true;
    }
}
