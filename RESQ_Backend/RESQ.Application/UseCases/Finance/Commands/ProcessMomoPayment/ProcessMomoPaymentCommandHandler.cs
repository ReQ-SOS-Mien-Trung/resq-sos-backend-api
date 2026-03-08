using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models.Finance.Momo;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using System.Security.Cryptography;
using System.Text;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessMomoPayment;

public class ProcessMomoPaymentCommandHandler : IRequestHandler<ProcessMomoPaymentCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDonationRepository _donationRepository;
    private readonly IFundCampaignRepository _campaignRepository;
    private readonly IFundTransactionRepository _transactionRepository;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProcessMomoPaymentCommandHandler> _logger;

    public ProcessMomoPaymentCommandHandler(
        IUnitOfWork unitOfWork,
        IDonationRepository donationRepository,
        IFundCampaignRepository campaignRepository,
        IFundTransactionRepository transactionRepository,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<ProcessMomoPaymentCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _donationRepository = donationRepository;
        _campaignRepository = campaignRepository;
        _transactionRepository = transactionRepository;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessMomoPaymentCommand request, CancellationToken cancellationToken)
    {
        var ipn = request.IpnData;
        if (!VerifyMomoSignature(ipn)) return false;

        var donation = await _donationRepository.GetByOrderIdAsync(ipn.OrderId, cancellationToken);
        if (donation == null) return false;

        try 
        {
            if (ipn.ResultCode == 0) // Success
            {
                // Business Rule: Update Status Check
                donation.UpdatePaymentStatus(Status.Succeed);

                donation.TransactionId = ipn.TransId.ToString();
                donation.PaymentAuditInfo = $"[MoMo:TransId={ipn.TransId},Type={ipn.PayType}]";
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
                        donation.FundCampaignName ?? "Campaign", donation.FundCampaignCode ?? "RESQ",
                        donation.Id, cancellationToken
                    );
                }
            }
            else
            {
                // Failed
                donation.UpdatePaymentStatus(Status.Failed);
                donation.PaymentAuditInfo += $" [MoMo Failed: {ipn.Message}]";
                await _donationRepository.UpdateAsync(donation, cancellationToken);
                await _unitOfWork.SaveAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MoMo donation logic");
            // If it's a domain logic exception (like trying to update a success transaction), we treat as processed.
            return true;
        }

        return true;
    }

    private bool VerifyMomoSignature(MomoIpnRequest ipn)
    {
        var config = _configuration.GetSection("MomoAPI");
        var accessKey = config["AccessKey"];
        var secretKey = config["SecretKey"];

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey)) return false;

        var rawData = $"accessKey={accessKey}&amount={ipn.Amount}&extraData={ipn.ExtraData}&message={ipn.Message}&orderId={ipn.OrderId}&orderInfo={ipn.OrderInfo}&orderType={ipn.OrderType}&partnerCode={ipn.PartnerCode}&payType={ipn.PayType}&requestId={ipn.RequestId}&responseTime={ipn.ResponseTime}&resultCode={ipn.ResultCode}&transId={ipn.TransId}";

        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var messageBytes = Encoding.UTF8.GetBytes(rawData);

        using (var hmac = new HMACSHA256(keyBytes))
        {
            var hashBytes = hmac.ComputeHash(messageBytes);
            var sb = new StringBuilder();
            foreach (var b in hashBytes) sb.Append(b.ToString("x2"));
            return sb.ToString() == ipn.Signature;
        }
    }
}

