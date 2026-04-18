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
        var entity = await _unitOfWork.Set<FundingRequest>()
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
        var query = _unitOfWork.Set<FundingRequest>()
            .Include(x => x.Depot)
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewedByUser)
            .Include(x => x.ApprovedCampaign)
            .AsQueryable();

        if (depotIds != null && depotIds.Count > 0)
            query = query.Where(x => depotIds.Contains(x.DepotId));

        if (statuses != null && statuses.Count > 0)
        {
            var statusesLower = statuses.Select(s => s.ToLower()).ToList();
            query = query.Where(x => statusesLower.Contains(x.Status.ToLower()));
        }

        query = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var models = entities.Select(FundingRequestMapper.ToModel).ToList();
        return new PagedResult<FundingRequestModel>(models, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<FundingRequestItemModel>> GetItemsPagedAsync(
        int fundingRequestId, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<FundingRequestItem>()
            .Where(x => x.FundingRequestId == fundingRequestId)
            .OrderBy(x => x.Row);

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var models = entities.Select(i => new FundingRequestItemModel
        {
            Id               = i.Id,
            FundingRequestId = i.FundingRequestId,
            Row              = i.Row,
            ItemName         = i.ItemName,
            CategoryCode     = i.CategoryCode,
            Unit             = i.Unit,
            Quantity         = i.Quantity,
            UnitPrice        = i.UnitPrice,
            TotalPrice       = i.TotalPrice,
            ItemType         = i.ItemType,
            TargetGroups     = string.IsNullOrEmpty(i.TargetGroup)
                                   ? new List<string>()
                                   : i.TargetGroup.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                  .Select(s => s.Trim()).ToList(),
            ReceivedDate     = i.ReceivedDate,
            ExpiredDate      = i.ExpiredDate,
            Notes            = i.Notes,
            VolumePerUnit    = i.VolumePerUnit,
            WeightPerUnit    = i.WeightPerUnit
        }).ToList();

        return new PagedResult<FundingRequestItemModel>(models, totalCount, pageNumber, pageSize);
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
        var entity = await _unitOfWork.SetTracked<FundingRequest>()
            .FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);

        if (entity != null)
        {
            FundingRequestMapper.UpdateEntity(entity, model);
        }
    }
}
