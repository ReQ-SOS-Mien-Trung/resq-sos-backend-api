using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Mappers.Finance;

namespace RESQ.Infrastructure.Persistence.Finance;

public class FundingRequestRepository : IFundingRequestRepository
{
    private readonly IUnitOfWork _unitOfWork;

    public FundingRequestRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<FundingRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<FundingRequest>().AsQueryable()
            .Include(x => x.FundingRequestItems)
            .Include(x => x.Depot)
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewedByUser)
            .Include(x => x.ApprovedCampaign)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity == null ? null : FundingRequestMapper.ToModel(entity);
    }

    public async Task<PagedResult<FundingRequestModel>> GetPagedAsync(
        int pageNumber, int pageSize,
        List<int>? depotIds = null, List<string>? statuses = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.GetRepository<FundingRequest>().AsQueryable()
            .Include(x => x.FundingRequestItems)
            .Include(x => x.Depot)
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewedByUser)
            .Include(x => x.ApprovedCampaign)
            .AsQueryable();

        if (depotIds != null && depotIds.Count > 0)
            query = query.Where(x => depotIds.Contains(x.DepotId));

        if (statuses != null && statuses.Count > 0)
            query = query.Where(x => statuses.Contains(x.Status));

        query = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var models = entities.Select(FundingRequestMapper.ToModel).ToList();
        return new PagedResult<FundingRequestModel>(models, totalCount, pageNumber, pageSize);
    }

    public async Task<int> CreateAsync(FundingRequestModel model, CancellationToken cancellationToken = default)
    {
        var entity = FundingRequestMapper.ToEntity(model);
        await _unitOfWork.GetRepository<FundingRequest>().AddAsync(entity);
        // Lưu ngay để DB sinh ra primary key, trả về ID thực
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task UpdateAsync(FundingRequestModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<FundingRequest>().AsQueryable(tracked: true)
            .FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);

        if (entity != null)
        {
            FundingRequestMapper.UpdateEntity(entity, model);
        }
    }
}
