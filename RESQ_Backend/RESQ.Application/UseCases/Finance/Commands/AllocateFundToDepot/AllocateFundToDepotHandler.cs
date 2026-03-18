using MediatR;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Finance.Services;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.AllocateFundToDepot;

/// <summary>
/// [Cách 1] Admin chủ động cấp tiền từ Campaign → Depot.
/// 1. Validate campaign (active, đủ tiền)
/// 2. Tạo CampaignDisbursement
/// 3. Tạo FundTransaction (ghi nhận dòng tiền ra)
/// </summary>
public class AllocateFundToDepotHandler : IRequestHandler<AllocateFundToDepotCommand, int>
{
    private readonly IFundCampaignRepository _campaignRepo;
    private readonly ICampaignDisbursementRepository _disbursementRepo;
    private readonly IFundTransactionRepository _transactionRepo;
    private readonly IFundDistributionManager _distributionManager;
    private readonly IUnitOfWork _unitOfWork;

    public AllocateFundToDepotHandler(
        IFundCampaignRepository campaignRepo,
        ICampaignDisbursementRepository disbursementRepo,
        IFundTransactionRepository transactionRepo,
        IFundDistributionManager distributionManager,
        IUnitOfWork unitOfWork)
    {
        _campaignRepo = campaignRepo;
        _disbursementRepo = disbursementRepo;
        _transactionRepo = transactionRepo;
        _distributionManager = distributionManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(AllocateFundToDepotCommand request, CancellationToken cancellationToken)
    {
        // 1. Lấy campaign
        var campaign = await _campaignRepo.GetByIdAsync(request.FundCampaignId, cancellationToken)
            ?? throw new RESQ.Application.Exceptions.NotFoundException($"Không tìm thấy chiến dịch #{request.FundCampaignId}.");

        // 2. Tính số dư khả dụng = TotalAmount - TotalDisbursed
        var totalDisbursed = await _disbursementRepo.GetTotalDisbursedByCampaignAsync(request.FundCampaignId, cancellationToken);
        var availableBalance = (campaign.TotalAmount ?? 0) - totalDisbursed;

        // 3. Domain Service validate (status, balance, amount)
        _distributionManager.ValidateAllocation(campaign, availableBalance, request.Amount);

        // 4. Tạo CampaignDisbursement — CreateAsync lưu ngay và trả về ID thực từ DB
        var disbursement = CampaignDisbursementModel.CreateAdminAllocation(
            request.FundCampaignId,
            request.DepotId,
            request.Amount,
            request.Purpose,
            request.AllocatedBy
        );
        var disbursementId = await _disbursementRepo.CreateAsync(disbursement, cancellationToken);

        // 5. Tạo FundTransaction (ghi nhận dòng tiền OUT)
        var transaction = new FundTransactionModel
        {
            FundCampaignId = request.FundCampaignId,
            Type = TransactionType.Allocation,
            Direction = "out",
            Amount = request.Amount,
            ReferenceType = TransactionReferenceType.CampaignDisbursement,
            ReferenceId = disbursementId,
            CreatedBy = request.AllocatedBy,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepo.CreateAsync(transaction, cancellationToken);

        await _unitOfWork.SaveAsync();

        return disbursementId;
    }
}
