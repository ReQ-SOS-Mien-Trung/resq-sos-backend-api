using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Mappers.Finance;

namespace RESQ.Infrastructure.Persistence.Finance;

public class CampaignDisbursementRepository : ICampaignDisbursementRepository
{
    private readonly IUnitOfWork _unitOfWork;

    public CampaignDisbursementRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CampaignDisbursementModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<CampaignDisbursement>().AsQueryable()
            .Include(x => x.DisbursementItems)
            .Include(x => x.FundCampaign)
            .Include(x => x.Depot)
            .Include(x => x.CreatedByUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity == null ? null : CampaignDisbursementMapper.ToModel(entity);
    }

    public async Task<PagedResult<CampaignDisbursementModel>> GetPagedAsync(
        int pageNumber, int pageSize,
        int? campaignId = null, int? depotId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.GetRepository<CampaignDisbursement>().AsQueryable()
            .Include(x => x.DisbursementItems)
            .Include(x => x.FundCampaign)
            .Include(x => x.Depot)
            .Include(x => x.CreatedByUser)
            .AsQueryable();

        if (campaignId.HasValue)
            query = query.Where(x => x.FundCampaignId == campaignId.Value);

        if (depotId.HasValue)
            query = query.Where(x => x.DepotId == depotId.Value);

        query = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var models = entities.Select(CampaignDisbursementMapper.ToModel).ToList();
        return new PagedResult<CampaignDisbursementModel>(models, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<CampaignDisbursementModel>> GetPublicByCampaignAsync(
        int campaignId, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.GetRepository<CampaignDisbursement>().AsQueryable()
            .Include(x => x.DisbursementItems)
            .Include(x => x.Depot)
            .Where(x => x.FundCampaignId == campaignId)
            .OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var models = entities.Select(CampaignDisbursementMapper.ToModel).ToList();
        return new PagedResult<CampaignDisbursementModel>(models, totalCount, pageNumber, pageSize);
    }

    public async Task<decimal> GetTotalDisbursedByCampaignAsync(int campaignId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.GetRepository<CampaignDisbursement>().AsQueryable()
            .Where(x => x.FundCampaignId == campaignId)
            .SumAsync(x => x.Amount, cancellationToken);
    }

    public async Task<int> CreateAsync(CampaignDisbursementModel model, CancellationToken cancellationToken = default)
    {
        var entity = CampaignDisbursementMapper.ToEntity(model);
        await _unitOfWork.GetRepository<CampaignDisbursement>().AddAsync(entity);
        // Lưu ngay để DB sinh ra primary key, trả về ID thực
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task AddItemsAsync(int disbursementId, List<DisbursementItemModel> items, CancellationToken cancellationToken = default)
    {
        var entities = items.Select(i => new DisbursementItem
        {
            CampaignDisbursementId = disbursementId,
            ItemName = i.ItemName,
            Unit = i.Unit,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            TotalPrice = i.TotalPrice,
            Note = i.Note,
            CreatedAt = i.CreatedAt
        }).ToList();

        await _unitOfWork.GetRepository<DisbursementItem>().AddRangeAsync(entities);
    }
}
