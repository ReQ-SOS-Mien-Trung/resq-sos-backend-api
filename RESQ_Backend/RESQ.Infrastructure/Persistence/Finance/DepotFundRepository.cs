using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Mappers.Finance;

namespace RESQ.Infrastructure.Persistence.Finance;

public class DepotFundRepository : IDepotFundRepository
{
    private readonly IUnitOfWork _unitOfWork;

    public DepotFundRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DepotFundModel?> GetByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotFund>().AsQueryable()
            .Include(x => x.Depot)
            .FirstOrDefaultAsync(x => x.DepotId == depotId, cancellationToken);

        return entity == null ? null : DepotFundMapper.ToModel(entity);
    }

    public async Task<DepotFundModel> GetOrCreateByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotFund>().AsQueryable(tracked: true)
            .Include(x => x.Depot)
            .FirstOrDefaultAsync(x => x.DepotId == depotId, cancellationToken);

        if (entity != null)
            return DepotFundMapper.ToModel(entity);

        // Lazy init: tạo mới với balance = 0
        var newFund = new DepotFund
        {
            DepotId = depotId,
            Balance = 0m,
            LastUpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.GetRepository<DepotFund>().AddAsync(newFund);
        await _unitOfWork.SaveAsync();

        // Reload with Depot include
        var saved = await _unitOfWork.GetRepository<DepotFund>().AsQueryable(tracked: true)
            .Include(x => x.Depot)
            .FirstAsync(x => x.Id == newFund.Id, cancellationToken);

        return DepotFundMapper.ToModel(saved);
    }

    public async Task<List<DepotFundModel>> GetAllWithDepotInfoAsync(CancellationToken cancellationToken = default)
    {
        // LEFT JOIN: tất cả depot, kể cả chưa có fund record
        var results = await _unitOfWork.GetRepository<Depot>().AsQueryable()
            .OrderBy(d => d.Id)
            .Select(d => new
            {
                d.Id,
                d.Name,
                Fund = d.DepotFund
            })
            .ToListAsync(cancellationToken);

        return results.Select(r =>
        {
            var model = r.Fund != null
                ? DepotFundModel.Reconstitute(r.Fund.Id, r.Fund.DepotId, r.Fund.Balance, r.Fund.LastUpdatedAt)
                : DepotFundModel.Reconstitute(0, r.Id, 0m, DateTime.MinValue);
            model.DepotName = r.Name;
            return model;
        }).ToList();
    }

    public async Task UpdateAsync(DepotFundModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<DepotFund>().AsQueryable(tracked: true)
            .FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);

        if (entity != null)
        {
            DepotFundMapper.UpdateEntity(entity, model);
        }
    }

    public async Task CreateTransactionAsync(DepotFundTransactionModel transaction, CancellationToken cancellationToken = default)
    {
        var entity = DepotFundTransactionMapper.ToEntity(transaction);
        await _unitOfWork.GetRepository<DepotFundTransaction>().AddAsync(entity);
    }

    public async Task<Dictionary<int, decimal>> GetBalancesByDepotIdsAsync(IEnumerable<int> depotIds, CancellationToken cancellationToken = default)
    {
        var ids = depotIds.ToList();
        if (ids.Count == 0) return new Dictionary<int, decimal>();

        return await _unitOfWork.GetRepository<DepotFund>().AsQueryable()
            .Where(x => ids.Contains(x.DepotId))
            .ToDictionaryAsync(x => x.DepotId, x => x.Balance, cancellationToken);
    }

    public async Task<PagedResult<DepotFundTransactionModel>> GetPagedTransactionsByDepotIdAsync(
        int depotId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.GetRepository<DepotFundTransaction>().AsQueryable()
            .Where(x => x.DepotFund.DepotId == depotId)
            .OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var models = items.Select(DepotFundTransactionMapper.ToModel).ToList();
        return new PagedResult<DepotFundTransactionModel>(models, totalCount, pageNumber, pageSize);
    }
}
