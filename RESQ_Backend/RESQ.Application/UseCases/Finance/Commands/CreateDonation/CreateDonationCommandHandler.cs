using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Finance.Exceptions;
using RESQ.Domain.Entities.Finance.ValueObjects;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.CreateDonation;

public class CreateDonationCommandHandler : IRequestHandler<CreateDonationCommand, CreateDonationResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDonationRepository _donationRepository;
    private readonly IFundCampaignRepository _fundCampaignRepository;
    private readonly IPaymentGatewayService _paymentGatewayService;

    public CreateDonationCommandHandler(
        IUnitOfWork unitOfWork,
        IDonationRepository donationRepository, 
        IFundCampaignRepository fundCampaignRepository,
        IPaymentGatewayService paymentGatewayService)
    {
        _unitOfWork = unitOfWork;
        _donationRepository = donationRepository;
        _fundCampaignRepository = fundCampaignRepository;
        _paymentGatewayService = paymentGatewayService;
    }

    public async Task<CreateDonationResponse> Handle(CreateDonationCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate Campaign
        var campaign = await _fundCampaignRepository.GetByIdAsync(request.FundCampaignId, cancellationToken);
        if (campaign == null)
        {
            throw new NotFoundException($"Không tìm thấy chiến dịch với ID {request.FundCampaignId}");
        }

        // Domain Rule Validation
        if (campaign.Status != FundCampaignStatus.Active)
        {
            throw new InvalidCampaignStatusException(
                campaign.Id, 
                campaign.Status.ToString(), 
                "Nhận ủng hộ"
            );
        }

        // 2. Create Initial Donation Record
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var donationModel = new DonationModel
        {
            FundCampaignId = request.FundCampaignId,
            
            // Value Objects
            Donor = new DonorInfo(request.DonorName, request.DonorEmail),
            Amount = new Money(request.Amount),
            
            Note = request.Note,
            PayosStatus = PayOSStatus.Pending,
            PayosOrderId = orderCode.ToString(),
            
            CreatedAt = DateTime.UtcNow,
            
            // Populate Domain Info for Service
            FundCampaignCode = campaign.Code,
            FundCampaignName = campaign.Name
        };

        await _donationRepository.CreateAsync(donationModel, cancellationToken);
        var saveResult = await _unitOfWork.SaveAsync();
        
        if (saveResult < 1)
        {
            throw new CreateFailedException("Đơn ủng hộ");
        }
        
        var addedDonation = await _donationRepository.GetByPayosOrderIdAsync(orderCode.ToString(), cancellationToken);
        if (addedDonation == null)
        {
            throw new Exception("Lỗi khi truy xuất đơn ủng hộ vừa tạo");
        }
        
        var donationId = addedDonation.Id;
        donationModel.Id = donationId;

        // 3. Call PayOS to generate Link
        var paymentResult = await _paymentGatewayService.CreatePaymentLinkAsync(
            donationModel, 
            cancellationToken);

        // 4. Update Donation with PayOS Info
        donationModel.PayosTransactionId = paymentResult.PaymentLinkId;
        
        await _donationRepository.UpdateAsync(donationModel, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new CreateDonationResponse
        {
            DonationId = donationId,
            CheckoutUrl = paymentResult.CheckoutUrl,
            QrCode = paymentResult.QrCode
        };
    }
}
