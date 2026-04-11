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
/// [Cách 1] Admin chủ động cấp tiền từ nguồn quỹ (Campaign hoặc SystemFund) → Depot.
/// 1. Validate nguồn (campaign đủ tiền / system fund đủ tiền)
/// 2. Trừ tiền nguồn
/// 3. Cộng quỹ kho tương ứng (depot fund gắn với nguồn cụ thể)
/// 4. Ghi log giao dịch + gửi Firebase notification
/// </summary>
public class AllocateFundToDepotHandler : IRequestHandler<AllocateFundToDepotCommand, int>
{
    private readonly IFundCampaignRepository _campaignRepo;
    private readonly ICampaignDisbursementRepository _disbursementRepo;
    private readonly IFundTransactionRepository _transactionRepo;
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IDepotRepository _depotRepo;
    private readonly IFundDistributionManager _distributionManager;
    private readonly ISystemFundRepository _systemFundRepo;
    private readonly IFirebaseService _firebaseService;
    private readonly IUnitOfWork _unitOfWork;

    public AllocateFundToDepotHandler(
        IFundCampaignRepository campaignRepo,
        ICampaignDisbursementRepository disbursementRepo,
        IFundTransactionRepository transactionRepo,
        IDepotFundRepository depotFundRepo,
        IDepotRepository depotRepo,
        IFundDistributionManager distributionManager,
        ISystemFundRepository systemFundRepo,
        IFirebaseService firebaseService,
        IUnitOfWork unitOfWork)
    {
        _campaignRepo = campaignRepo;
        _disbursementRepo = disbursementRepo;
        _transactionRepo = transactionRepo;
        _depotFundRepo = depotFundRepo;
        _depotRepo = depotRepo;
        _distributionManager = distributionManager;
        _systemFundRepo = systemFundRepo;
        _firebaseService = firebaseService;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(AllocateFundToDepotCommand request, CancellationToken cancellationToken)
    {
        return request.SourceType switch
        {
            FundSourceType.Campaign => await HandleCampaignAllocation(request, cancellationToken),
            FundSourceType.SystemFund => await HandleSystemFundAllocation(request, cancellationToken),
            _ => throw new RESQ.Application.Exceptions.BadRequestException($"Loại nguồn quỹ không hợp lệ: {request.SourceType}.")
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Campaign → Depot (giữ nguyên luồng cũ, cộng quỹ vào depot fund gắn campaign)
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<int> HandleCampaignAllocation(AllocateFundToDepotCommand request, CancellationToken cancellationToken)
    {
        if (!request.FundCampaignId.HasValue)
            throw new RESQ.Application.Exceptions.BadRequestException("FundCampaignId là bắt buộc khi nguồn quỹ là Campaign.");

        var campaignId = request.FundCampaignId.Value;

        // 1. Lấy campaign
        var campaign = await _campaignRepo.GetByIdAsync(campaignId, cancellationToken)
            ?? throw new RESQ.Application.Exceptions.NotFoundException($"Không tìm thấy chiến dịch #{campaignId}.");

        var availableBalance = campaign.CurrentBalance ?? 0;
        _distributionManager.ValidateAllocation(campaign, availableBalance, request.Amount);

        // 2. Tạo CampaignDisbursement
        var disbursement = CampaignDisbursementModel.CreateAdminAllocation(
            campaignId, request.DepotId, request.Amount, request.Purpose, request.AllocatedBy);
        var disbursementId = await _disbursementRepo.CreateAsync(disbursement, cancellationToken);

        campaign.Disburse(request.Amount, request.AllocatedBy);
        await _campaignRepo.UpdateAsync(campaign, cancellationToken);

        // 3. Tạo FundTransaction (OUT)
        await _transactionRepo.CreateAsync(new FundTransactionModel
        {
            FundCampaignId = campaignId,
            Type = TransactionType.Allocation,
            Direction = TransactionDirection.Out,
            Amount = request.Amount,
            ReferenceType = TransactionReferenceType.CampaignDisbursement,
            ReferenceId = disbursementId,
            CreatedBy = request.AllocatedBy,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        // 4. Cộng quỹ kho gắn với campaign (tạo mới nếu chưa có)
        var depotFund = await _depotFundRepo.GetOrCreateByDepotAndSourceAsync(
            request.DepotId, FundSourceType.Campaign, campaignId, cancellationToken);
        depotFund.Credit(request.Amount);
        await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

        // 5. Ghi log giao dịch quỹ kho — Allocation
        await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
        {
            DepotFundId = depotFund.Id,
            TransactionType = DepotFundTransactionType.Allocation,
            Amount = request.Amount,
            ReferenceType = "CampaignDisbursement",
            ReferenceId = disbursementId,
            Note = $"Cấp quỹ từ chiến dịch #{campaignId}",
            CreatedBy = request.AllocatedBy,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveAsync();

        // 6. Firebase notification
        await NotifyDepotManager(request.DepotId, request.Amount, $"chiến dịch \"{campaign.Name}\"", cancellationToken);

        return disbursementId;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SystemFund → Depot
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<int> HandleSystemFundAllocation(AllocateFundToDepotCommand request, CancellationToken cancellationToken)
    {
        // 1. Lấy quỹ hệ thống
        var systemFund = await _systemFundRepo.GetOrCreateAsync(cancellationToken);
        if (request.Amount > systemFund.Balance)
            throw new RESQ.Application.Exceptions.ConflictException(
                $"Quỹ hệ thống không đủ số dư. Hiện có: {systemFund.Balance:N0} VNĐ, Yêu cầu: {request.Amount:N0} VNĐ.");

        // 2. Trừ quỹ hệ thống
        systemFund.Debit(request.Amount);
        await _systemFundRepo.UpdateAsync(systemFund, cancellationToken);

        // 3. Ghi log giao dịch quỹ hệ thống (OUT)
        await _systemFundRepo.CreateTransactionAsync(new SystemFundTransactionModel
        {
            SystemFundId = systemFund.Id,
            TransactionType = SystemFundTransactionType.AllocationToDepot,
            Amount = request.Amount,
            ReferenceType = "DepotFundAllocation",
            ReferenceId = null,
            Note = $"Cấp {request.Amount:N0} VNĐ cho kho #{request.DepotId} từ quỹ hệ thống",
            CreatedBy = request.AllocatedBy,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        // 4. Cộng quỹ kho gắn với SystemFund (tạo mới nếu chưa có)
        var depotFund = await _depotFundRepo.GetOrCreateByDepotAndSourceAsync(
            request.DepotId, FundSourceType.SystemFund, null, cancellationToken);
        depotFund.Credit(request.Amount);
        await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

        // 5. Ghi log giao dịch quỹ kho
        await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
        {
            DepotFundId = depotFund.Id,
            TransactionType = DepotFundTransactionType.Allocation,
            Amount = request.Amount,
            ReferenceType = "SystemFund",
            ReferenceId = systemFund.Id,
            Note = $"Cấp quỹ từ quỹ hệ thống",
            CreatedBy = request.AllocatedBy,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveAsync();

        // 6. Firebase notification
        await NotifyDepotManager(request.DepotId, request.Amount, "quỹ hệ thống", cancellationToken);

        return depotFund.Id;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task NotifyDepotManager(int depotId, decimal amount, string sourceName, CancellationToken ct)
    {
        var depot = await _depotRepo.GetByIdAsync(depotId, ct);
        var managerId = depot?.CurrentManagerId;
        if (managerId.HasValue)
        {
            var depotName = depot!.Name ?? $"Kho #{depotId}";
            await _firebaseService.SendNotificationToUserAsync(
                managerId.Value,
                "Quỹ kho được cấp mới",
                $"Kho {depotName} vừa được cấp {amount:N0} VNĐ từ {sourceName}.",
                "fund_allocation",
                ct);
        }
    }
}
