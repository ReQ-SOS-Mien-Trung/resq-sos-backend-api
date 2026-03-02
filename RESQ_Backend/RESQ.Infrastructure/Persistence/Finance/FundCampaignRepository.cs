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
        var entity = await repo.GetByPropertyAsync(x => x.Id == id, tracked: false);
        return entity == null ? null : FundCampaignMapper.ToModel(entity);
    }

    public async Task<FundCampaignModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<FundCampaign>();
        var entity = await repo.GetByPropertyAsync(x => x.Code == code, tracked: false);
        return entity == null ? null : FundCampaignMapper.ToModel(entity);
    }

    public async Task<PagedResult<FundCampaignModel>> GetPagedAsync(int pageNumber, int pageSize, string? status = null, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<FundCampaign>();
        Expression<Func<FundCampaign, bool>>? filter = null;

        if (!string.IsNullOrEmpty(status))
        {
            filter = x => x.Status == status;
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
            // Map updates
            entity.Name = model.Name;
            entity.Region = model.Region;
            entity.CampaignStartDate = model.CampaignStartDate;
            entity.CampaignEndDate = model.CampaignEndDate;
            entity.TargetAmount = model.TargetAmount;
            
            // IMPORTANT: Update Total Amount
            entity.TotalAmount = model.TotalAmount;
            
            entity.Status = model.Status.ToString();
            
            await repo.UpdateAsync(entity);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<FundCampaign>();
        await repo.DeleteAsyncById(id);
    }
}
