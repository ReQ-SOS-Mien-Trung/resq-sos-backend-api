using MediatR;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Finance.Services;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.ApproveFundingRequest;

/// <summary>
/// Admin duyệt FundingRequest:
/// 1. Validate FundingRequest (Pending)
/// 2. Validate Campaign (Active, đủ tiền)
/// 3. Approve FundingRequest → gán campaignId
/// 4. Tạo CampaignDisbursement (Type = FundingRequestApproval)
/// 5. Tạo FundTransaction (OUT)
/// </summary>
public class ApproveFundingRequestHandler : IRequestHandler<ApproveFundingRequestCommand, int>
{
    private readonly IFundingRequestRepository _fundingRequestRepo;
    private readonly IFundCampaignRepository _campaignRepo;
    private readonly ICampaignDisbursementRepository _disbursementRepo;
    private readonly IFundTransactionRepository _transactionRepo;
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IFundDistributionManager _distributionManager;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveFundingRequestHandler(
        IFundingRequestRepository fundingRequestRepo,
        IFundCampaignRepository campaignRepo,
        ICampaignDisbursementRepository disbursementRepo,
        IFundTransactionRepository transactionRepo,
        IDepotFundRepository depotFundRepo,
        IFundDistributionManager distributionManager,
        IUnitOfWork unitOfWork)
    {
        _fundingRequestRepo = fundingRequestRepo;
        _campaignRepo = campaignRepo;
        _disbursementRepo = disbursementRepo;
        _transactionRepo = transactionRepo;
        _depotFundRepo = depotFundRepo;
        _distributionManager = distributionManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(ApproveFundingRequestCommand request, CancellationToken cancellationToken)
    {
        // 1. Lấy FundingRequest
        var fundingRequest = await _fundingRequestRepo.GetByIdAsync(request.FundingRequestId, cancellationToken)
            ?? throw new RESQ.Application.Exceptions.NotFoundException(
                $"Không tìm thấy yêu cầu cấp quỹ #{request.FundingRequestId}.");

        // 2. Lấy Campaign
        var campaign = await _campaignRepo.GetByIdAsync(request.CampaignId, cancellationToken)
            ?? throw new RESQ.Application.Exceptions.NotFoundException(
                $"Không tìm thấy chiến dịch #{request.CampaignId}.");

        // 3. Tính số dư khả dụng
        var totalDisbursed = await _disbursementRepo.GetTotalDisbursedByCampaignAsync(request.CampaignId, cancellationToken);
        var availableBalance = (campaign.TotalAmount ?? 0) - totalDisbursed;

        // 4. Domain Service validate (campaign active, đủ tiền)
        _distributionManager.ValidateAllocation(campaign, availableBalance, fundingRequest.TotalAmount);

        // 5. Domain logic: Approve request
        fundingRequest.Approve(request.CampaignId, request.ReviewedBy);
        await _fundingRequestRepo.UpdateAsync(fundingRequest, cancellationToken);

        // 6. Tạo CampaignDisbursement — CreateAsync lưu ngay và trả về ID thực từ DB
        //    (chưa có items — items thực tế ghi khi depot nhập hàng qua ImportPurchasedInventory)
        var disbursement = CampaignDisbursementModel.CreateFromFundingRequest(
            request.CampaignId,
            fundingRequest.DepotId,
            fundingRequest.TotalAmount,
            fundingRequest.Id,
            request.ReviewedBy
        );
        var disbursementId = await _disbursementRepo.CreateAsync(disbursement, cancellationToken);

        // 6b. Trừ số tiền đã giải ngân khỏi TotalAmount của chiến dịch
        campaign.Disburse(fundingRequest.TotalAmount, request.ReviewedBy);
        await _campaignRepo.UpdateAsync(campaign, cancellationToken);

        // 7. Tạo FundTransaction (OUT)
        var transaction = new FundTransactionModel
        {
            FundCampaignId = request.CampaignId,
            Type = TransactionType.Allocation,
            Direction = "out",
            Amount = fundingRequest.TotalAmount,
            ReferenceType = TransactionReferenceType.CampaignDisbursement,
            ReferenceId = disbursementId,
            CreatedBy = request.ReviewedBy,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepo.CreateAsync(transaction, cancellationToken);

        // 8. Cộng quỹ kho (lazy init nếu chưa có)
        var depotFund = await _depotFundRepo.GetOrCreateByDepotIdAsync(fundingRequest.DepotId, cancellationToken);
        depotFund.Credit(fundingRequest.TotalAmount);
        await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

        // 9. Ghi log giao dịch quỹ kho
        await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
        {
            DepotFundId = depotFund.Id,
            TransactionType = DepotFundTransactionType.Allocation,
            Amount = fundingRequest.TotalAmount,
            ReferenceType = "CampaignDisbursement",
            ReferenceId = disbursementId,
            Note = $"Duyệt yêu cầu cấp quỹ #{fundingRequest.Id} từ chiến dịch #{request.CampaignId}",
            CreatedBy = request.ReviewedBy,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveAsync();

        return disbursementId;
    }
}
