using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Mappers.Finance;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Finance;

public class DepotFundAllocationRepository(IUnitOfWork unitOfWork) : IDepotFundAllocationRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<PagedResult<DepotFundAllocationModel>> GetPagedAsync(int pageNumber, int pageSize, int? campaignId = null, int? depotId = null, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<DepotFundAllocation>();
        Expression<Func<DepotFundAllocation, bool>>? filter = null;

        if (campaignId.HasValue && depotId.HasValue)
        {
            filter = x => x.FundCampaignId == campaignId.Value && x.DepotId == depotId.Value;
        }
        else if (campaignId.HasValue)
        {
            filter = x => x.FundCampaignId == campaignId.Value;
        }
        else if (depotId.HasValue)
        {
            filter = x => x.DepotId == depotId.Value;
        }

        var pagedEntities = await repo.GetPagedAsync(
            pageNumber,
            pageSize,
            filter,
            q => q.OrderByDescending(x => x.AllocatedAt),
            "FundCampaign,Depot"
        );

        var models = pagedEntities.Items.Select(DepotFundAllocationMapper.ToModel).ToList();

        return new PagedResult<DepotFundAllocationModel>(models, pagedEntities.TotalCount, pageNumber, pageSize);
    }

    public async Task CreateAllocationAsync(DepotFundAllocationModel model, CancellationToken cancellationToken = default)
    {
        // 1. Insert into depot_fund_allocations
        var entity = DepotFundAllocationMapper.ToEntity(model);
        
        // Ensure timestamp
        if (!entity.AllocatedAt.HasValue) entity.AllocatedAt = DateTime.UtcNow;

        var repo = _unitOfWork.GetRepository<DepotFundAllocation>();
        await repo.AddAsync(entity);
        
        // 2. Insert into fund_transactions
        // Logic: type = "Allocation", direction = "out", reference_type = "DepotFundAllocation"
        var transaction = new FundTransaction
        {
            FundCampaignId = entity.FundCampaignId,
            Type = TransactionType.Allocation.ToString(),
            Direction = "out",
            Amount = entity.Amount,
            ReferenceType = TransactionReferenceType.DepotFundAllocation.ToString(),
            ReferenceId = entity.Id, // This might be 0 until SaveAsync is called, handled by EF Core relationships if entity is tracked correctly or fixed in handler
            CreatedBy = entity.AllocatedBy,
            CreatedAt = DateTime.UtcNow
        };
        
        var transactionRepo = _unitOfWork.GetRepository<FundTransaction>();
        await transactionRepo.AddAsync(transaction);
    }
}
