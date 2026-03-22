using MediatR;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Finance.Services;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.AllocateFundToDepot;

/// <summary>
/// [Cách 1] Admin chủ động cấp tiền từ Campaign → Depot.
/// 1. Validate campaign (active, đủ tiền)
/// 2. Tạo CampaignDisbursement
/// 3. Tạo FundTransaction (ghi nhận dòng tiền ra)
/// 4. Cộng quỹ kho + gửi Firebase notification cho manager
/// </summary>
public class AllocateFundToDepotHandler : IRequestHandler<AllocateFundToDepotCommand, int>
{
    private readonly IFundCampaignRepository _campaignRepo;
    private readonly ICampaignDisbursementRepository _disbursementRepo;
    private readonly IFundTransactionRepository _transactionRepo;
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IDepotRepository _depotRepo;
    private readonly IFundDistributionManager _distributionManager;
    private readonly IFirebaseService _firebaseService;
    private readonly IUnitOfWork _unitOfWork;

    public AllocateFundToDepotHandler(
        IFundCampaignRepository campaignRepo,
        ICampaignDisbursementRepository disbursementRepo,
        IFundTransactionRepository transactionRepo,
        IDepotFundRepository depotFundRepo,
        IDepotRepository depotRepo,
        IFundDistributionManager distributionManager,
        IFirebaseService firebaseService,
        IUnitOfWork unitOfWork)
    {
        _campaignRepo = campaignRepo;
        _disbursementRepo = disbursementRepo;
        _transactionRepo = transactionRepo;
        _depotFundRepo = depotFundRepo;
        _depotRepo = depotRepo;
        _distributionManager = distributionManager;
        _firebaseService = firebaseService;
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

        // 4b. Trừ số tiền đã cấp khỏi TotalAmount của chiến dịch
        campaign.Disburse(request.Amount, request.AllocatedBy);
        await _campaignRepo.UpdateAsync(campaign, cancellationToken);

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

        // 6. Cộng quỹ kho (lazy init nếu chưa có) — tự động trừ nợ nếu balance âm
        var depotFund = await _depotFundRepo.GetOrCreateByDepotIdAsync(request.DepotId, cancellationToken);
        var creditResult = depotFund.Credit(request.Amount);
        await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

        // 7. Ghi log giao dịch quỹ kho — Allocation
        await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
        {
            DepotFundId = depotFund.Id,
            TransactionType = DepotFundTransactionType.Allocation,
            Amount = request.Amount,
            ReferenceType = "CampaignDisbursement",
            ReferenceId = disbursementId,
            Note = $"Cấp quỹ từ chiến dịch #{request.FundCampaignId}",
            CreatedBy = request.AllocatedBy,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        // 7b. Ghi thêm transaction trừ nợ nếu kho đang tự ứng (âm)
        if (creditResult.DebtRepaid > 0)
        {
            await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
            {
                DepotFundId = depotFund.Id,
                TransactionType = DepotFundTransactionType.DebtRepayment,
                Amount = creditResult.DebtRepaid,
                ReferenceType = "CampaignDisbursement",
                ReferenceId = disbursementId,
                Note = $"Trừ {creditResult.DebtRepaid:N0} VNĐ nợ kho đã tự ứng trước đó",
                CreatedBy = request.AllocatedBy,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        await _unitOfWork.SaveAsync();

        // 8. Gửi Firebase notification cho manager hiện tại của kho
        var depot = await _depotRepo.GetByIdAsync(request.DepotId, cancellationToken);
        var managerId = depot?.CurrentManagerId;
        if (managerId.HasValue)
        {
            var depotName = depot!.Name ?? $"Kho #{request.DepotId}";
            await _firebaseService.SendNotificationToUserAsync(
                managerId.Value,
                "Quỹ kho được cấp mới",
                $"Kho {depotName} vừa được cấp {request.Amount:N0} VNĐ từ chiến dịch \"{campaign.Name}\".",
                "fund_allocation",
                cancellationToken);
        }

        return disbursementId;
    }
}
