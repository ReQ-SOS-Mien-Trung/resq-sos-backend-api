using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models.Finance.Momo;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Finance;
using System.Security.Cryptography;
using System.Text;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessMomoPayment;

public class ProcessMomoPaymentCommandHandler : IRequestHandler<ProcessMomoPaymentCommand, bool>
{
    private readonly IDonationRepository _donationRepository;
    private readonly IDonationPaymentProcessingService _donationPaymentProcessingService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProcessMomoPaymentCommandHandler> _logger;

    public ProcessMomoPaymentCommandHandler(
        IDonationRepository donationRepository,
        IDonationPaymentProcessingService donationPaymentProcessingService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<ProcessMomoPaymentCommandHandler> logger)
    {
        _donationRepository = donationRepository;
        _donationPaymentProcessingService = donationPaymentProcessingService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessMomoPaymentCommand request, CancellationToken cancellationToken)
    {
        var ipn = request.IpnData;
        
        _logger.LogInformation("MoMo IPN Init - OrderId: {OrderId}, TransId: {TransId}, ResultCode: {ResultCode}", ipn.OrderId, ipn.TransId, ipn.ResultCode);

        if (!VerifyMomoSignature(ipn)) 
        {
            _logger.LogWarning("MoMo IPN Signature Verification Failed for OrderId: {OrderId}", ipn.OrderId);
            return false;
        }

        var donation = await _donationRepository.GetByOrderIdAsync(ipn.OrderId, cancellationToken);
        if (donation == null) 
        {
            _logger.LogWarning("MoMo IPN Failed - Donation not found for OrderId: {OrderId}", ipn.OrderId);
            return false;
        }

        var wasSucceededBefore = donation.Status == Status.Succeed;

        try 
        {
            if (ipn.ResultCode == 0) // Success
            {
                var processed = await _donationPaymentProcessingService.TryProcessSuccessAsync(
                    donation.Id,
                    $"[MoMo:TransId={ipn.TransId},Type={ipn.PayType}]",
                    DateTime.UtcNow,
                    ipn.TransId.ToString(),
                    preserveExistingTransactionId: false,
                    cancellationToken);

                if (!processed)
                {
                    return false;
                }

                if (!wasSucceededBefore && donation.Donor != null && !string.IsNullOrEmpty(donation.Donor.Email))
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
                var processed = await _donationPaymentProcessingService.TryProcessFailureAsync(
                    donation.Id,
                    $"[MoMo Failed: {ipn.Message}]",
                    cancellationToken);

                if (!processed)
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MoMo donation logic");
            return false;
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

