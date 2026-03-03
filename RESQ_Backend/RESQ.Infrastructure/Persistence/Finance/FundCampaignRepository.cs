using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Mappers.Finance;

namespace RESQ.Infrastructure.Persistence.Finance;

public class FundCampaignRepository(IUnitOfWork unitOfWork) : IFundCampaignRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<FundCampaignModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<FundCampaign>();
        // Ignore deleted
        var entity = await repo.GetByPropertyAsync(x => x.Id == id && !x.IsDeleted, tracked: false);
        return entity == null ? null : FundCampaignMapper.ToModel(entity);
    }

    public async Task<FundCampaignModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<FundCampaign>();
        var entity = await repo.GetByPropertyAsync(x => x.Code == code && !x.IsDeleted, tracked: false);
        return entity == null ? null : FundCampaignMapper.ToModel(entity);
    }

    public async Task<PagedResult<FundCampaignModel>> GetPagedAsync(int pageNumber, int pageSize, string? status = null, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<FundCampaign>();
        Expression<Func<FundCampaign, bool>> filter = x => !x.IsDeleted;

        if (!string.IsNullOrEmpty(status))
        {
            var parameter = Expression.Parameter(typeof(FundCampaign), "x");
            var statusProp = Expression.Property(parameter, nameof(FundCampaign.Status));
            var deletedProp = Expression.Property(parameter, nameof(FundCampaign.IsDeleted));
            
            var statusConst = Expression.Constant(status);
            var falseConst = Expression.Constant(false);

            var statusCheck = Expression.Equal(statusProp, statusConst);
            var deletedCheck = Expression.Equal(deletedProp, falseConst);
            var combined = Expression.AndAlso(statusCheck, deletedCheck);

            filter = Expression.Lambda<Func<FundCampaign, bool>>(combined, parameter);
        }

        var pagedEntities = await repo.GetPagedAsync(
            pageNumber,
            pageSize,
            filter,
            q => q.OrderByDescending(x => x.CreatedAt)
        );

        var models = pagedEntities.Items.Select(FundCampaignMapper.ToModel).ToList();
        return new PagedResult<FundCampaignModel>(models, pagedEntities.TotalCount, pageNumber, pageSize);
    }

    public async Task CreateAsync(FundCampaignModel model, CancellationToken cancellationToken = default)
    {
        var entity = FundCampaignMapper.ToEntity(model);
        var repo = _unitOfWork.GetRepository<FundCampaign>();
        await repo.AddAsync(entity);
    }

    public async Task UpdateAsync(FundCampaignModel model, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<FundCampaign>();
        var entity = await repo.GetByPropertyAsync(x => x.Id == model.Id);
        
        if (entity != null)
        {
            FundCampaignMapper.UpdateEntity(entity, model);
            await repo.UpdateAsync(entity);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<FundCampaign>();
        var entity = await repo.GetByPropertyAsync(x => x.Id == id);
        
        if (entity != null)
        {
            // Soft Delete Implementation
            entity.IsDeleted = true;
            await repo.UpdateAsync(entity);
        }
    }
}
