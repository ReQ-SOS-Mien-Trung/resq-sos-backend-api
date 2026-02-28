using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Mappers.Finance;

namespace RESQ.Infrastructure.Persistence.Finance;

public class FundTransactionRepository(IUnitOfWork unitOfWork) : IFundTransactionRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<PagedResult<FundTransactionModel>> GetByCampaignIdAsync(int campaignId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<FundTransaction>();
        
        var pagedEntities = await repo.GetPagedAsync(
            pageNumber,
            pageSize,
            x => x.FundCampaignId == campaignId,
            q => q.OrderByDescending(x => x.CreatedAt),
            "CreatedByUser"
        );

        var models = pagedEntities.Items.Select(FundTransactionMapper.ToModel).ToList();
        return new PagedResult<FundTransactionModel>(models, pagedEntities.TotalCount, pageNumber, pageSize);
    }

    public async Task CreateAsync(FundTransactionModel model, CancellationToken cancellationToken = default)
    {
        var entity = FundTransactionMapper.ToEntity(model);
        var repo = _unitOfWork.GetRepository<FundTransaction>();
        await repo.AddAsync(entity);
    }
}
