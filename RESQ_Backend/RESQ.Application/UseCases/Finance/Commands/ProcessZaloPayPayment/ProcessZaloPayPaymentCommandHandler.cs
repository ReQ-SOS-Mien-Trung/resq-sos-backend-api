using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models.Finance.ZaloPay;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using System.Text.Json;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessZaloPayPayment;

public class ProcessZaloPayPaymentCommandHandler : IRequestHandler<ProcessZaloPayPaymentCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDonationRepository _donationRepository;
    private readonly IFundCampaignRepository _campaignRepository;
    private readonly IFundTransactionRepository _transactionRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<ProcessZaloPayPaymentCommandHandler> _logger;

    public ProcessZaloPayPaymentCommandHandler(
        IUnitOfWork unitOfWork,
        IDonationRepository donationRepository,
        IFundCampaignRepository campaignRepository,
        IFundTransactionRepository transactionRepository,
        IEmailService emailService,
        ILogger<ProcessZaloPayPaymentCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _donationRepository = donationRepository;
        _campaignRepository = campaignRepository;
        _transactionRepository = transactionRepository;
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

            // Mark success
            donation.UpdatePaymentStatus(Status.Succeed);
            donation.TransactionId = dataJson.ZpTransId.ToString();
            donation.PaymentAuditInfo = $"[ZaloPay:ZpTransId={dataJson.ZpTransId}]";
            donation.PaidAt = DateTimeOffset.FromUnixTimeMilliseconds(dataJson.ServerTime).UtcDateTime;

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
                    donation.FundCampaignName ?? "Campaign", donation.FundCampaignCode ?? "RESQ",
                    donation.Id, cancellationToken
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
