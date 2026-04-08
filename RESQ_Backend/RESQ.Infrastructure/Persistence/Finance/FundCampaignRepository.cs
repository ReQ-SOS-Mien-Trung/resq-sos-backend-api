using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
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

    public async Task<PagedResult<FundCampaignModel>> GetPagedAsync(int pageNumber, int pageSize, List<FundCampaignStatus>? statuses = null, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<FundCampaign>()
            .Where(x => !x.IsDeleted)
            .AsQueryable();

        if (statuses != null && statuses.Count > 0)
        {
            var statusStrings = statuses.Select(s => s.ToString()).ToList();
            query = query.Where(x => statusStrings.Contains(x.Status!));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var entities = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var models = entities.Select(FundCampaignMapper.ToModel).ToList();
        return new PagedResult<FundCampaignModel>(models, totalCount, pageNumber, pageSize);
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

    public async Task<List<FundCampaignModel>> GetExpiredActiveAsync(CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<FundCampaign>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var entities = await repo.GetAllByPropertyAsync(
            x => !x.IsDeleted
              && x.Status == "Active"
              && x.CampaignEndDate.HasValue
              && x.CampaignEndDate.Value < today);

        return entities.Select(FundCampaignMapper.ToModel).ToList();
    }
}
