using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Finance.Exceptions;
using RESQ.Domain.Entities.Finance.Services;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.AllocateFundToDepot;

public class AllocateFundToDepotHandler(
    IFundCampaignRepository campaignRepo,
    ICampaignDisbursementRepository disbursementRepo,
    IFundTransactionRepository transactionRepo,
    IDepotFundRepository depotFundRepo,
    IDepotRepository depotRepo,
    IFundDistributionManager distributionManager,
    ISystemFundRepository systemFundRepo,
    IFirebaseService firebaseService,
    IAdminRealtimeHubService adminRealtimeHubService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AllocateFundToDepotCommand, int>
{
    private const int MaxConcurrencyRetries = 3;

    private readonly IFundCampaignRepository _campaignRepo = campaignRepo;
    private readonly ICampaignDisbursementRepository _disbursementRepo = disbursementRepo;
    private readonly IFundTransactionRepository _transactionRepo = transactionRepo;
    private readonly IDepotFundRepository _depotFundRepo = depotFundRepo;
    private readonly IDepotRepository _depotRepo = depotRepo;
    private readonly IFundDistributionManager _distributionManager = distributionManager;
    private readonly ISystemFundRepository _systemFundRepo = systemFundRepo;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<int> Handle(AllocateFundToDepotCommand request, CancellationToken cancellationToken)
    {
        return request.SourceType switch
        {
            FundSourceType.Campaign => await ExecuteWithConcurrencyRetryAsync(
                () => HandleCampaignAllocationCoreAsync(request, cancellationToken)),
            FundSourceType.SystemFund => await ExecuteWithConcurrencyRetryAsync(
                () => HandleSystemFundAllocationCoreAsync(request, cancellationToken)),
            _ => throw new BadRequestException($"Loại nguồn quỹ không hợp lệ: {request.SourceType}.")
        };
    }

    private async Task<int> HandleCampaignAllocationCoreAsync(
        AllocateFundToDepotCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.FundCampaignId.HasValue)
        {
            throw new BadRequestException("FundCampaignId là bắt buộc khi nguồn quỹ là Campaign.");
        }

        var campaignId = request.FundCampaignId.Value;
        var disbursementId = 0;
        var campaignStatus = string.Empty;
        string campaignName = $"chiến dịch #{campaignId}";

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var campaign = await _campaignRepo.GetByIdAsync(campaignId, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy chiến dịch #{campaignId}.");

            campaignName = string.IsNullOrWhiteSpace(campaign.Name)
                ? $"chiến dịch #{campaignId}"
                : $"chiến dịch \"{campaign.Name}\"";

            _distributionManager.ValidateAllocation(campaign, campaign.CurrentBalance ?? 0m, request.Amount);

            var disbursementReference = await _disbursementRepo.CreateAsync(
                CampaignDisbursementModel.CreateAdminAllocation(
                    campaignId,
                    request.DepotId,
                    request.Amount,
                    request.Purpose,
                    request.AllocatedBy),
                cancellationToken);

            await _unitOfWork.SaveAsync();
            disbursementId = disbursementReference.CurrentId;

            campaign.Disburse(request.Amount, request.AllocatedBy);
            await _campaignRepo.UpdateAsync(campaign, cancellationToken);
            campaignStatus = campaign.Status.ToString();

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

            var depotFund = await _depotFundRepo.GetOrCreateByDepotAndSourceAsync(
                request.DepotId,
                FundSourceType.Campaign,
                campaignId,
                cancellationToken);

            if (depotFund.Id == 0)
            {
                await _unitOfWork.SaveAsync();
                depotFund = await _depotFundRepo.GetOrCreateByDepotAndSourceAsync(
                    request.DepotId,
                    FundSourceType.Campaign,
                    campaignId,
                    cancellationToken);
            }

            depotFund.Credit(request.Amount);
            await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

            await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
            {
                DepotFundId = depotFund.Id,
                TransactionType = DepotFundTransactionType.Allocation,
                Amount = request.Amount,
                ReferenceType = DepotFundReferenceType.CampaignDisbursement.ToString(),
                ReferenceId = disbursementId,
                Note = $"Cấp quỹ từ chiến dịch #{campaignId}",
                CreatedBy = request.AllocatedBy,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _unitOfWork.SaveAsync();
        });

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
                DepotId = request.DepotId,
                Amount = request.Amount,
                Action = "CreatedByAdminAllocation",
                Status = DisbursementType.AdminAllocation.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);

        await NotifyDepotManager(request.DepotId, request.Amount, campaignName, cancellationToken);
        return disbursementId;
    }

    private async Task<int> HandleSystemFundAllocationCoreAsync(
        AllocateFundToDepotCommand request,
        CancellationToken cancellationToken)
    {
        var depotFundId = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var systemFund = await _systemFundRepo.GetOrCreateAsync(cancellationToken);
            if (systemFund.Id == 0)
            {
                await _unitOfWork.SaveAsync();
                systemFund = await _systemFundRepo.GetOrCreateAsync(cancellationToken);
            }

            if (request.Amount > systemFund.Balance)
            {
                throw new ConflictException(
                    $"Quỹ hệ thống không đủ số dư. Hiện có: {systemFund.Balance:N0} VNĐ, Yêu cầu: {request.Amount:N0} VNĐ.");
            }

            systemFund.Debit(request.Amount);
            await _systemFundRepo.UpdateAsync(systemFund, cancellationToken);

            await _systemFundRepo.CreateTransactionAsync(new SystemFundTransactionModel
            {
                SystemFundId = systemFund.Id,
                TransactionType = SystemFundTransactionType.AllocationToDepot,
                Amount = request.Amount,
                ReferenceType = DepotFundReferenceType.SystemFund.ToString(),
                ReferenceId = systemFund.Id,
                Note = $"Cấp {request.Amount:N0} VNĐ cho kho #{request.DepotId} từ quỹ hệ thống",
                CreatedBy = request.AllocatedBy,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            var depotFund = await _depotFundRepo.GetOrCreateByDepotAndSourceAsync(
                request.DepotId,
                FundSourceType.SystemFund,
                null,
                cancellationToken);

            if (depotFund.Id == 0)
            {
                await _unitOfWork.SaveAsync();
                depotFund = await _depotFundRepo.GetOrCreateByDepotAndSourceAsync(
                    request.DepotId,
                    FundSourceType.SystemFund,
                    null,
                    cancellationToken);
            }

            depotFund.Credit(request.Amount);
            await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

            await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
            {
                DepotFundId = depotFund.Id,
                TransactionType = DepotFundTransactionType.Allocation,
                Amount = request.Amount,
                ReferenceType = DepotFundReferenceType.SystemFund.ToString(),
                ReferenceId = systemFund.Id,
                Note = "Cấp quỹ từ quỹ hệ thống",
                CreatedBy = request.AllocatedBy,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _unitOfWork.SaveAsync();
            depotFundId = depotFund.Id;
        });

        await _adminRealtimeHubService.PushDisbursementUpdateAsync(
            new AdminDisbursementRealtimeUpdate
            {
                EntityId = depotFundId,
                EntityType = "Disbursement",
                DisbursementId = null,
                CampaignId = null,
                DepotId = request.DepotId,
                Amount = request.Amount,
                Action = "CreatedByAdminAllocation",
                Status = FundSourceType.SystemFund.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);

        await NotifyDepotManager(request.DepotId, request.Amount, "quỹ hệ thống", cancellationToken);
        return depotFundId;
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
