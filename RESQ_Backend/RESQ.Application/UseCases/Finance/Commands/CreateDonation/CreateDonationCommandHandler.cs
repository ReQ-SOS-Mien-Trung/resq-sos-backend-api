using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Common.Constants;
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
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;

    public CreateDonationCommandHandler(
        IUnitOfWork unitOfWork,
        IDonationRepository donationRepository, 
        IFundCampaignRepository fundCampaignRepository,
        IPaymentGatewayFactory paymentGatewayFactory)
    {
        _unitOfWork = unitOfWork;
        _donationRepository = donationRepository;
        _fundCampaignRepository = fundCampaignRepository;
        _paymentGatewayFactory = paymentGatewayFactory;
    }

    public async Task<CreateDonationResponse> Handle(CreateDonationCommand request, CancellationToken cancellationToken)
    {
        // 1. Parse và validate PaymentMethodCode (FluentValidation đã chặn chuỗi không hợp lệ,
        //    nhưng parse lại ở đây để có typed enum cho toàn bộ handler)
        if (!Enum.TryParse<PaymentMethodCode>(request.PaymentMethodCode, ignoreCase: true, out var paymentCode))
            throw new BadRequestException($"Phương thức thanh toán '{request.PaymentMethodCode}' không hợp lệ.");

        if (paymentCode.IsHidden())
            throw new BadRequestException($"Phương thức thanh toán '{paymentCode.GetDescription()}' hiện chưa được hỗ trợ. Vui lòng chọn PayOS hoặc ZaloPay.");

        // 2. Validate Campaign
        var campaign = await _fundCampaignRepository.GetByIdAsync(request.FundCampaignId, cancellationToken);
        if (campaign == null)
            throw new NotFoundException($"Không tìm thấy chiến dịch với ID {request.FundCampaignId}");

        if (campaign.Status != FundCampaignStatus.Active)
            throw new InvalidCampaignStatusException(campaign.Id, campaign.Status.ToString(), "Nhận ủng hộ");

        // 3. Create Donation (dùng enum paymentCode - không quay lại dùng string)
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var now = DateTime.UtcNow;
        var donationModel = new DonationModel
        {
            FundCampaignId = request.FundCampaignId,
            Donor = new DonorInfo(request.DonorName, request.DonorEmail),
            Amount = new Money(request.Amount),
            Note = request.Note,
            IsPrivate = request.IsPrivate,
            OrderId = orderCode.ToString(),
            PaymentMethodCode = paymentCode,
            CreatedAt = now,
            ResponseDeadline = DonationPaymentConstants.CalculateResponseDeadlineUtc(now),
            FundCampaignCode = campaign.Code,
            FundCampaignName = campaign.Name
        };

        donationModel.SetStatus(Status.Pending);

        await _donationRepository.CreateAsync(donationModel, cancellationToken);
        if (await _unitOfWork.SaveAsync() < 1)
            throw new CreateFailedException("đơn ủng hộ");
        
        var addedDonation = await _donationRepository.GetByOrderIdAsync(orderCode.ToString(), cancellationToken);
        if (addedDonation == null) throw new Exception("Lỗi khi truy xuất đơn ủng hộ.");

        // 4. Resolve Gateway Service bằng enum (type-safe)
        addedDonation.FundCampaignCode = campaign.Code;
        
        var paymentService = _paymentGatewayFactory.GetService(paymentCode);
        var paymentResult = await paymentService.CreatePaymentLinkAsync(addedDonation, cancellationToken);

        // 5. Update Transaction Info
        addedDonation.TransactionId = paymentResult.PaymentLinkId;
        await _donationRepository.UpdateAsync(addedDonation, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new CreateDonationResponse
        {
            DonationId = addedDonation.Id,
            CheckoutUrl = paymentResult.CheckoutUrl,
            QrCode = paymentResult.QrCode,
            PaymentMethod = paymentCode.GetDescription(),
            OrderId = addedDonation.OrderId ?? string.Empty
        };
    }
}
