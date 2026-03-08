using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using System.Globalization;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessPayosPaymentReturn;

public class ProcessPayosPaymentReturnCommandHandler : IRequestHandler<ProcessPayosPaymentReturnCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDonationRepository _donationRepository;
    private readonly IFundCampaignRepository _campaignRepository;
    private readonly IFundTransactionRepository _transactionRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<ProcessPayosPaymentReturnCommandHandler> _logger;

    public ProcessPayosPaymentReturnCommandHandler(
        IUnitOfWork unitOfWork,
        IDonationRepository donationRepository,
        IFundCampaignRepository campaignRepository,
        IFundTransactionRepository transactionRepository,
        IEmailService emailService,
        ILogger<ProcessPayosPaymentReturnCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _donationRepository = donationRepository;
        _campaignRepository = campaignRepository;
        _transactionRepository = transactionRepository;
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
            return true;
        }

        try 
        {
            // Business Rule: Update Status Check (Enforces Idempotency and State rules)
            donation.UpdatePaymentStatus(Status.Succeed);

            donation.TransactionId = paymentLinkId;
            donation.PaymentAuditInfo = $"[Bank:{webhook.Data.CounterAccountBankName}-{webhook.Data.CounterAccountNumber}]";
            
            if (DateTime.TryParseExact(webhook.Data.TransactionDateTime, new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var paidAt))
                donation.PaidAt = paidAt.ToUniversalTime();
            else
                donation.PaidAt = DateTime.UtcNow;
            
            await _donationRepository.UpdateAsync(donation, cancellationToken);

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
                        Direction = "in",
                        Amount = donation.Amount?.Amount,
                        ReferenceType = TransactionReferenceType.Donation,
                        ReferenceId = donation.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _transactionRepository.CreateAsync(transaction, cancellationToken);
                }
            }

            await _unitOfWork.SaveAsync();

            if (donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
            {
                _ = _emailService.SendDonationSuccessEmailAsync(
                    donation.Donor.Email, donation.Donor.Name, donation.Amount?.Amount ?? 0,
                    donation.FundCampaignName ?? "Chiáº¿n dá»‹ch", donation.FundCampaignCode ?? "RESQ",
                    donation.Id, cancellationToken
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

