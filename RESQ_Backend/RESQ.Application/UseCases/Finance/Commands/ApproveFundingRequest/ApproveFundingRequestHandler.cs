using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Finance.Exceptions;
using RESQ.Domain.Entities.Finance.Services;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.ApproveFundingRequest;

public class ApproveFundingRequestHandler(
    IFundingRequestRepository fundingRequestRepo,
    IFundCampaignRepository campaignRepo,
    ICampaignDisbursementRepository disbursementRepo,
    IFundTransactionRepository transactionRepo,
    IDepotFundRepository depotFundRepo,
    ISystemFundRepository systemFundRepo,
    IFundDistributionManager distributionManager,
    IAdminRealtimeHubService adminRealtimeHubService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ApproveFundingRequestCommand, int>
{
    private const int MaxConcurrencyRetries = 3;

    private readonly IFundingRequestRepository _fundingRequestRepo = fundingRequestRepo;
    private readonly IFundCampaignRepository _campaignRepo = campaignRepo;
    private readonly ICampaignDisbursementRepository _disbursementRepo = disbursementRepo;
    private readonly IFundTransactionRepository _transactionRepo = transactionRepo;
    private readonly IDepotFundRepository _depotFundRepo = depotFundRepo;
    private readonly ISystemFundRepository _systemFundRepo = systemFundRepo;
    private readonly IFundDistributionManager _distributionManager = distributionManager;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<int> Handle(ApproveFundingRequestCommand request, CancellationToken cancellationToken)
    {
        return request.SourceType switch
        {
            FundSourceType.Campaign => await ExecuteWithConcurrencyRetryAsync(
                () => HandleCampaignApprovalCoreAsync(request, cancellationToken)),
            FundSourceType.SystemFund => await ExecuteWithConcurrencyRetryAsync(
                () => HandleSystemFundApprovalCoreAsync(request, cancellationToken)),
            _ => throw new BadRequestException(
                $"Loại nguồn quỹ không hợp lệ: {request.SourceType}.")
        };
    }

    private async Task<int> HandleCampaignApprovalCoreAsync(
        ApproveFundingRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.CampaignId.HasValue)
        {
            throw new BadRequestException("CampaignId là bắt buộc khi nguồn quỹ là Campaign.");
        }

        var campaignId = request.CampaignId.Value;
        var disbursementId = 0;
        var campaignStatus = string.Empty;
        FundingRequestModel? fundingRequest = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            fundingRequest = await _fundingRequestRepo.GetByIdAsync(request.FundingRequestId, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy yêu cầu cấp quỹ #{request.FundingRequestId}.");

            var campaign = await _campaignRepo.GetByIdAsync(campaignId, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy chiến dịch #{campaignId}.");

            _distributionManager.ValidateAllocation(campaign, campaign.CurrentBalance ?? 0m, fundingRequest.TotalAmount);

            fundingRequest.Approve(campaignId, request.ReviewedBy);
            await _fundingRequestRepo.UpdateAsync(fundingRequest, cancellationToken);

            var disbursementReference = await _disbursementRepo.CreateAsync(
                CampaignDisbursementModel.CreateFromFundingRequest(
                    campaignId,
                    fundingRequest.DepotId,
                    fundingRequest.TotalAmount,
                    fundingRequest.Id,
                    request.ReviewedBy),
                cancellationToken);

            await _unitOfWork.SaveAsync();
            disbursementId = disbursementReference.CurrentId;

            campaign.Disburse(fundingRequest.TotalAmount, request.ReviewedBy);
            await _campaignRepo.UpdateAsync(campaign, cancellationToken);
            campaignStatus = campaign.Status.ToString();

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

            var depotFund = await _depotFundRepo.GetOrCreateByDepotAndSourceAsync(
                fundingRequest.DepotId,
                FundSourceType.Campaign,
                campaignId,
                cancellationToken);

            if (depotFund.Id == 0)
            {
                await _unitOfWork.SaveAsync();
                depotFund = await _depotFundRepo.GetOrCreateByDepotAndSourceAsync(
                    fundingRequest.DepotId,
                    FundSourceType.Campaign,
                    campaignId,
                    cancellationToken);
            }

            depotFund.Credit(fundingRequest.TotalAmount);
            await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

            await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
            {
                DepotFundId = depotFund.Id,
                TransactionType = DepotFundTransactionType.Allocation,
                Amount = fundingRequest.TotalAmount,
                ReferenceType = DepotFundReferenceType.CampaignDisbursement.ToString(),
                ReferenceId = disbursementId,
                Note = $"Duyệt yêu cầu cấp quỹ #{fundingRequest.Id} từ chiến dịch #{campaignId}",
                CreatedBy = request.ReviewedBy,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _unitOfWork.SaveAsync();
        });

        await PushCampaignApprovalRealtimeAsync(
            fundingRequest!,
            campaignId,
            campaignStatus,
            disbursementId,
            cancellationToken);

        return disbursementId;
    }

    private async Task<int> HandleSystemFundApprovalCoreAsync(
        ApproveFundingRequestCommand request,
        CancellationToken cancellationToken)
    {
        FundingRequestModel? fundingRequest = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            fundingRequest = await _fundingRequestRepo.GetByIdAsync(request.FundingRequestId, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy yêu cầu cấp quỹ #{request.FundingRequestId}.");

            var systemFund = await _systemFundRepo.GetOrCreateAsync(cancellationToken);
            if (systemFund.Id == 0)
            {
                await _unitOfWork.SaveAsync();
                systemFund = await _systemFundRepo.GetOrCreateAsync(cancellationToken);
            }

            if (fundingRequest.TotalAmount > systemFund.Balance)
            {
                throw new ConflictException(
                    $"Quỹ hệ thống không đủ số dư. Hiện có: {systemFund.Balance:N0} VNĐ, Yêu cầu: {fundingRequest.TotalAmount:N0} VNĐ.");
            }

            fundingRequest.Approve(null, request.ReviewedBy);
            await _fundingRequestRepo.UpdateAsync(fundingRequest, cancellationToken);

            systemFund.Debit(fundingRequest.TotalAmount);
            await _systemFundRepo.UpdateAsync(systemFund, cancellationToken);

            await _systemFundRepo.CreateTransactionAsync(new SystemFundTransactionModel
            {
                SystemFundId = systemFund.Id,
                TransactionType = SystemFundTransactionType.AllocationToDepot,
                Amount = fundingRequest.TotalAmount,
                ReferenceType = DepotFundReferenceType.FundingRequest.ToString(),
                ReferenceId = fundingRequest.Id,
                Note = $"Duyệt yêu cầu cấp quỹ #{fundingRequest.Id} — cấp {fundingRequest.TotalAmount:N0} VNĐ cho kho #{fundingRequest.DepotId}",
                CreatedBy = request.ReviewedBy,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            var depotFund = await _depotFundRepo.GetOrCreateByDepotAndSourceAsync(
                fundingRequest.DepotId,
                FundSourceType.SystemFund,
                null,
                cancellationToken);

            if (depotFund.Id == 0)
            {
                await _unitOfWork.SaveAsync();
                depotFund = await _depotFundRepo.GetOrCreateByDepotAndSourceAsync(
                    fundingRequest.DepotId,
                    FundSourceType.SystemFund,
                    null,
                    cancellationToken);
            }

            depotFund.Credit(fundingRequest.TotalAmount);
            await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

            await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
            {
                DepotFundId = depotFund.Id,
                TransactionType = DepotFundTransactionType.Allocation,
                Amount = fundingRequest.TotalAmount,
                ReferenceType = DepotFundReferenceType.FundingRequest.ToString(),
                ReferenceId = fundingRequest.Id,
                Note = $"Duyệt yêu cầu cấp quỹ #{fundingRequest.Id} từ quỹ hệ thống",
                CreatedBy = request.ReviewedBy,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _unitOfWork.SaveAsync();
        });

        await _adminRealtimeHubService.PushFundingRequestUpdateAsync(
            new AdminFundingRequestRealtimeUpdate
            {
                EntityId = fundingRequest!.Id,
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
                EntityId = fundingRequest!.Id,
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

        return 0;
    }

    private async Task<int> ExecuteWithConcurrencyRetryAsync(Func<Task<int>> action)
    {
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            try
            {
                return await action();
            }
            catch (ConflictException) when (attempt < MaxConcurrencyRetries)
            {
                _unitOfWork.ClearTrackedChanges();
            }
            catch (ConflictException)
            {
                _unitOfWork.ClearTrackedChanges();
                throw new ConcurrentFinanceMutationException();
            }
        }

        throw new ConcurrentFinanceMutationException();
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
