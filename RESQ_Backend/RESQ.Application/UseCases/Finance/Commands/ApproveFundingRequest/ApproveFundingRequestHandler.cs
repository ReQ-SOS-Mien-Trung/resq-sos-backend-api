using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Finance.Services;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.ApproveFundingRequest;

/// <summary>
/// Admin duyệt FundingRequest - chọn nguồn quỹ:
/// (A) Campaign  → validate campaign, trừ campaign, tạo CampaignDisbursement, cộng depot fund
/// (B) SystemFund → validate system fund, trừ system fund, cộng depot fund
/// </summary>
public class ApproveFundingRequestHandler : IRequestHandler<ApproveFundingRequestCommand, int>
{
    private readonly IFundingRequestRepository _fundingRequestRepo;
    private readonly IFundCampaignRepository _campaignRepo;
    private readonly ICampaignDisbursementRepository _disbursementRepo;
    private readonly IFundTransactionRepository _transactionRepo;
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly ISystemFundRepository _systemFundRepo;
    private readonly IFundDistributionManager _distributionManager;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveFundingRequestHandler(
        IFundingRequestRepository fundingRequestRepo,
        IFundCampaignRepository campaignRepo,
        ICampaignDisbursementRepository disbursementRepo,
        IFundTransactionRepository transactionRepo,
        IDepotFundRepository depotFundRepo,
        ISystemFundRepository systemFundRepo,
        IFundDistributionManager distributionManager,
        IAdminRealtimeHubService adminRealtimeHubService,
        IUnitOfWork unitOfWork)
    {
        _fundingRequestRepo = fundingRequestRepo;
        _campaignRepo = campaignRepo;
        _disbursementRepo = disbursementRepo;
        _transactionRepo = transactionRepo;
        _depotFundRepo = depotFundRepo;
        _systemFundRepo = systemFundRepo;
        _distributionManager = distributionManager;
        _adminRealtimeHubService = adminRealtimeHubService;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(ApproveFundingRequestCommand request, CancellationToken cancellationToken)
    {
        // 1. Lấy FundingRequest
        var fundingRequest = await _fundingRequestRepo.GetByIdAsync(request.FundingRequestId, cancellationToken)
            ?? throw new RESQ.Application.Exceptions.NotFoundException(
                $"Không tìm thấy yêu cầu cấp quỹ #{request.FundingRequestId}.");

        return request.SourceType switch
        {
            FundSourceType.Campaign => await HandleCampaignApproval(request, fundingRequest, cancellationToken),
            FundSourceType.SystemFund => await HandleSystemFundApproval(request, fundingRequest, cancellationToken),
            _ => throw new RESQ.Application.Exceptions.BadRequestException($"Loại nguồn quỹ không hợp lệ: {request.SourceType}.")
        };
    }

    //  Campaign → Depot (luồng cũ)

    private async Task<int> HandleCampaignApproval(
        ApproveFundingRequestCommand request,
        FundingRequestModel fundingRequest,
        CancellationToken cancellationToken)
    {
        if (!request.CampaignId.HasValue)
            throw new RESQ.Application.Exceptions.BadRequestException("CampaignId là bắt buộc khi nguồn quỹ là Campaign.");

        var campaignId = request.CampaignId.Value;

        // 2. Lấy Campaign
        var campaign = await _campaignRepo.GetByIdAsync(campaignId, cancellationToken)
            ?? throw new RESQ.Application.Exceptions.NotFoundException(
                $"Không tìm thấy chiến dịch #{campaignId}.");

        // 3. Số dư khả dụng
        var availableBalance = campaign.CurrentBalance ?? 0;
        _distributionManager.ValidateAllocation(campaign, availableBalance, fundingRequest.TotalAmount);

        // 4. Approve FundingRequest
        fundingRequest.Approve(campaignId, request.ReviewedBy);
        await _fundingRequestRepo.UpdateAsync(fundingRequest, cancellationToken);

        // 5. Tạo CampaignDisbursement
        var disbursement = CampaignDisbursementModel.CreateFromFundingRequest(
            campaignId,
            fundingRequest.DepotId,
            fundingRequest.TotalAmount,
            fundingRequest.Id,
            request.ReviewedBy
        );
        var disbursementId = await _disbursementRepo.CreateAsync(disbursement, cancellationToken);

        // 6. Trừ campaign balance
        campaign.Disburse(fundingRequest.TotalAmount, request.ReviewedBy);
        await _campaignRepo.UpdateAsync(campaign, cancellationToken);

        // 7. FundTransaction (OUT)
        await _transactionRepo.CreateAsync(new FundTransactionModel
        {
            FundCampaignId = campaignId,
            Type = TransactionType.Allocation,
            Direction = TransactionDirection.Out,
            Amount = fundingRequest.TotalAmount,
            ReferenceType = TransactionReferenceType.CampaignDisbursement,
            ReferenceId = disbursementId,
            CreatedBy = request.ReviewedBy,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        // 8. Cộng quỹ kho gắn campaign
        var depotFund = await _depotFundRepo.GetOrCreateByDepotAndSourceAsync(
            fundingRequest.DepotId, FundSourceType.Campaign, campaignId, cancellationToken);
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
            Note = $"Duyệt yêu cầu cấp quỹ #{fundingRequest.Id} từ chiến dịch #{campaignId}",
            CreatedBy = request.ReviewedBy,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveAsync();
        await PushCampaignApprovalRealtimeAsync(
            fundingRequest,
            campaignId,
            campaign.Status.ToString(),
            disbursementId,
            cancellationToken);
        return disbursementId;
    }

    //  SystemFund → Depot

    private async Task<int> HandleSystemFundApproval(
        ApproveFundingRequestCommand request,
        FundingRequestModel fundingRequest,
        CancellationToken cancellationToken)
    {
        // 1. Lấy quỹ hệ thống
        var systemFund = await _systemFundRepo.GetOrCreateAsync(cancellationToken);
        if (fundingRequest.TotalAmount > systemFund.Balance)
            throw new RESQ.Application.Exceptions.ConflictException(
                $"Quỹ hệ thống không đủ số dư. Hiện có: {systemFund.Balance:N0} VNĐ, Yêu cầu: {fundingRequest.TotalAmount:N0} VNĐ.");

        // 2. Approve FundingRequest (không gắn campaignId)
        fundingRequest.Approve(null, request.ReviewedBy);
        await _fundingRequestRepo.UpdateAsync(fundingRequest, cancellationToken);

        // 3. Trừ quỹ hệ thống
        systemFund.Debit(fundingRequest.TotalAmount);
        await _systemFundRepo.UpdateAsync(systemFund, cancellationToken);

        // 4. Ghi log giao dịch quỹ hệ thống
        await _systemFundRepo.CreateTransactionAsync(new SystemFundTransactionModel
        {
            SystemFundId = systemFund.Id,
            TransactionType = SystemFundTransactionType.AllocationToDepot,
            Amount = fundingRequest.TotalAmount,
            ReferenceType = "FundingRequest",
            ReferenceId = fundingRequest.Id,
            Note = $"Duyệt yêu cầu cấp quỹ #{fundingRequest.Id} — cấp {fundingRequest.TotalAmount:N0} VNĐ cho kho #{fundingRequest.DepotId}",
            CreatedBy = request.ReviewedBy,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        // 5. Cộng quỹ kho gắn SystemFund
        var depotFund = await _depotFundRepo.GetOrCreateByDepotAndSourceAsync(
            fundingRequest.DepotId, FundSourceType.SystemFund, null, cancellationToken);
        depotFund.Credit(fundingRequest.TotalAmount);
        await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

        // 6. Ghi log giao dịch quỹ kho
        await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
        {
            DepotFundId = depotFund.Id,
            TransactionType = DepotFundTransactionType.Allocation,
            Amount = fundingRequest.TotalAmount,
            ReferenceType = "SystemFund",
            ReferenceId = systemFund.Id,
            Note = $"Duyệt yêu cầu cấp quỹ #{fundingRequest.Id} từ quỹ hệ thống",
            CreatedBy = request.ReviewedBy,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveAsync();
        await _adminRealtimeHubService.PushFundingRequestUpdateAsync(
            new AdminFundingRequestRealtimeUpdate
            {
                EntityId = fundingRequest.Id,
                EntityType = "FundingRequest",
                RequestId = fundingRequest.Id,
                DepotId = fundingRequest.DepotId,
                Action = "Approved",
                Status = fundingRequest.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);
        await _adminRealtimeHubService.PushDisbursementUpdateAsync(
            new AdminDisbursementRealtimeUpdate
            {
                EntityId = fundingRequest.Id,
                EntityType = "Disbursement",
                DisbursementId = null,
                CampaignId = null,
                DepotId = fundingRequest.DepotId,
                Amount = fundingRequest.TotalAmount,
                Action = "AllocatedFromSystemFund",
                Status = FundSourceType.SystemFund.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);

        // Trả về 0 vì không tạo CampaignDisbursement (quỹ hệ thống không có disbursement record)
        return 0;
    }

    private async Task PushCampaignApprovalRealtimeAsync(
        FundingRequestModel fundingRequest,
        int campaignId,
        string campaignStatus,
        int disbursementId,
        CancellationToken cancellationToken)
    {
        await _adminRealtimeHubService.PushFundingRequestUpdateAsync(
            new AdminFundingRequestRealtimeUpdate
            {
                EntityId = fundingRequest.Id,
                EntityType = "FundingRequest",
                RequestId = fundingRequest.Id,
                DepotId = fundingRequest.DepotId,
                Action = "Approved",
                Status = fundingRequest.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);

        await _adminRealtimeHubService.PushCampaignUpdateAsync(
            new AdminCampaignRealtimeUpdate
            {
                EntityId = campaignId,
                EntityType = "Campaign",
                CampaignId = campaignId,
                Action = "FundsDisbursed",
                Status = campaignStatus,
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);

        await _adminRealtimeHubService.PushDisbursementUpdateAsync(
            new AdminDisbursementRealtimeUpdate
            {
                EntityId = disbursementId,
                EntityType = "Disbursement",
                DisbursementId = disbursementId,
                CampaignId = campaignId,
                DepotId = fundingRequest.DepotId,
                Amount = fundingRequest.TotalAmount,
                Action = "CreatedFromFundingRequestApproval",
                Status = DisbursementType.FundingRequestApproval.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);
    }
}
