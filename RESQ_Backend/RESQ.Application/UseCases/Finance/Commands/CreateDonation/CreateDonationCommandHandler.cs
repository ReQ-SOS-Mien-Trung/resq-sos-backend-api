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
    private readonly IPaymentMethodRepository _paymentMethodRepository;
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;

    public CreateDonationCommandHandler(
        IUnitOfWork unitOfWork,
        IDonationRepository donationRepository, 
        IFundCampaignRepository fundCampaignRepository,
        IPaymentMethodRepository paymentMethodRepository,
        IPaymentGatewayFactory paymentGatewayFactory)
    {
        _unitOfWork = unitOfWork;
        _donationRepository = donationRepository;
        _fundCampaignRepository = fundCampaignRepository;
        _paymentMethodRepository = paymentMethodRepository;
        _paymentGatewayFactory = paymentGatewayFactory;
    }

    public async Task<CreateDonationResponse> Handle(CreateDonationCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate Campaign
        var campaign = await _fundCampaignRepository.GetByIdAsync(request.FundCampaignId, cancellationToken);
        if (campaign == null)
            throw new NotFoundException($"Không tìm thấy chiến dịch với ID {request.FundCampaignId}");

        if (campaign.Status != FundCampaignStatus.Active)
            throw new InvalidCampaignStatusException(campaign.Id, campaign.Status.ToString(), "Nhận ủng hộ");

        // 2. Validate Payment Method from DB
        var paymentMethodEntity = await _paymentMethodRepository.GetByIdAsync(request.PaymentMethodId, cancellationToken);
        if (paymentMethodEntity == null || !paymentMethodEntity.IsActive)
            throw new BadRequestException("Phương thức thanh toán không hợp lệ hoặc đã ngưng hoạt động.");

        // 3. Create Donation
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var donationModel = new DonationModel
        {
            FundCampaignId = request.FundCampaignId,
            Donor = new DonorInfo(request.DonorName, request.DonorEmail),
            Amount = new Money(request.Amount),
            Note = request.Note,
            IsPrivate = request.IsPrivate,
            PayosOrderId = orderCode.ToString(),
            PaymentMethodId = request.PaymentMethodId,
            CreatedAt = DateTime.UtcNow,
            FundCampaignCode = campaign.Code,
            FundCampaignName = campaign.Name
        };

        donationModel.SetStatus(PayOSStatus.Pending);

        await _donationRepository.CreateAsync(donationModel, cancellationToken);
        if (await _unitOfWork.SaveAsync() < 1)
            throw new CreateFailedException("Đơn ủng hộ");
        
        var addedDonation = await _donationRepository.GetByPayosOrderIdAsync(orderCode.ToString(), cancellationToken);
        if (addedDonation == null) throw new Exception("Lỗi khi truy xuất đơn ủng hộ.");

        // 4. Resolve Gateway Service using the Code from DB Entity
        addedDonation.FundCampaignCode = campaign.Code;
        
        var paymentService = _paymentGatewayFactory.GetService(paymentMethodEntity.Code);
        var paymentResult = await paymentService.CreatePaymentLinkAsync(addedDonation, cancellationToken);

        // 5. Update Transaction Info
        addedDonation.PayosTransactionId = paymentResult.PaymentLinkId;
        await _donationRepository.UpdateAsync(addedDonation, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new CreateDonationResponse
        {
            DonationId = addedDonation.Id,
            CheckoutUrl = paymentResult.CheckoutUrl,
            QrCode = paymentResult.QrCode,
            PaymentMethod = paymentMethodEntity.Name
        };
    }
}
