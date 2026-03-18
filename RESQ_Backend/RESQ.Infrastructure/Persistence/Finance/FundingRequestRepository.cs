using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Mappers.Finance;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Finance;

public class FundingRequestRepository : IFundingRequestRepository
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ResQDbContext _dbContext;

    public FundingRequestRepository(IUnitOfWork unitOfWork, ResQDbContext dbContext)
    {
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
    }

    public async Task<FundingRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.FundingRequests
            .Include(x => x.FundingRequestItems)
            .Include(x => x.Depot)
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewedByUser)
            .Include(x => x.ApprovedCampaign)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity == null ? null : FundingRequestMapper.ToModel(entity);
    }

    public async Task<PagedResult<FundingRequestModel>> GetPagedAsync(
        int pageNumber, int pageSize,
        int? depotId = null, string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.FundingRequests
            .Include(x => x.FundingRequestItems)
            .Include(x => x.Depot)
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewedByUser)
            .Include(x => x.ApprovedCampaign)
            .AsNoTracking()
            .AsQueryable();

        if (depotId.HasValue)
            query = query.Where(x => x.DepotId == depotId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(x => x.Status == status);

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
        await _dbContext.FundingRequests.AddAsync(entity, cancellationToken);
        // Lưu ngay để DB sinh ra primary key, trả về ID thực
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(FundingRequestModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.FundingRequests
            .FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);

        if (entity != null)
        {
            FundingRequestMapper.UpdateEntity(entity, model);
        }
    }
}
