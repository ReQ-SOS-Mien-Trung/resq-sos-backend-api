using MediatR;
using Microsoft.Extensions.Logging;
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

        // 1. Find Donation by Order Code
        var donation = await _donationRepository.GetByPayosOrderIdAsync(orderCodeStr, cancellationToken);
        if (donation == null)
        {
            _logger.LogError("Donation not found for OrderCode: {OrderCode}", orderCodeStr);
            return true;
        }

        // 2. Idempotency Check
        if (donation.PayosStatus == PayOSStatus.Succeed)
        {
            _logger.LogInformation("Donation {Id} (OrderCode: {OrderCode}) is already marked as Succeed. Ignoring duplicate webhook.", 
                donation.Id, orderCodeStr);
            return true;
        }

        try 
        {
            // 3. Determine New Status
            PayOSStatus newStatus = PayOSStatus.Succeed;

            // 4. Update Donation Data
            donation.PayosStatus = newStatus;
            donation.PayosTransactionId = paymentLinkId;
            
            // UPDATED: Save audit info to dedicated column instead of Note
            var auditInfo = $"[Bank:{webhook.Data.CounterAccountBankName}-{webhook.Data.CounterAccountNumber}]";
            donation.PaymentAuditInfo = auditInfo; 
            // Note field remains untouched (keeps user message)

            // Parse Date Time from Webhook
            if (DateTime.TryParseExact(
                    webhook.Data.TransactionDateTime,
                    new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm" }, 
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
            
            // Update Donation in Context
            await _donationRepository.UpdateAsync(donation, cancellationToken);

            // 5. Update Campaign & Add Transaction Ledger
            if (donation.FundCampaignId.HasValue)
            {
                var campaign = await _campaignRepository.GetByIdAsync(donation.FundCampaignId.Value, cancellationToken);
                
                if (campaign != null && !campaign.IsDeleted)
                {
                    var donationAmount = donation.Amount?.Amount ?? 0;
                    if (donationAmount > 0)
                    {
                        campaign.ReceiveDonation(donationAmount);
                        await _campaignRepository.UpdateAsync(campaign, cancellationToken);
                        
                        var transaction = new FundTransactionModel
                        {
                            FundCampaignId = donation.FundCampaignId,
                            Type = TransactionType.Donation,
                            Direction = "in",
                            Amount = donationAmount,
                            ReferenceType = TransactionReferenceType.Donation,
                            ReferenceId = donation.Id,
                            CreatedBy = null, // System
                            CreatedAt = DateTime.UtcNow,
                        };
                        await _transactionRepository.CreateAsync(transaction, cancellationToken);
                    }
                }
                else 
                {
                    _logger.LogWarning("Campaign {CampaignId} not found or deleted for Donation {DonationId}", donation.FundCampaignId, donation.Id);
                }
            }

            // 6. Commit Transaction
            var rowsAffected = await _unitOfWork.SaveAsync();
            _logger.LogInformation("Donation {Id} updated to Succeed. Rows affected: {Rows}", donation.Id, rowsAffected);

            // 7. Send Email
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
                    _logger.LogError(ex, "Failed to send success email for Donation {Id}", donation.Id);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing donation logic for OrderCode {OrderCode}", orderCodeStr);
            return false; 
        }
    }
}
