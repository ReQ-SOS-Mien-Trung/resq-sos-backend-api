using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Mappers.Finance;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Finance;

public class DepotFundRepository : IDepotFundRepository
{
    private readonly ResQDbContext _dbContext;

    public DepotFundRepository(ResQDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DepotFundModel?> GetByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.DepotFunds
            .Include(x => x.Depot)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DepotId == depotId, cancellationToken);

        return entity == null ? null : DepotFundMapper.ToModel(entity);
    }

    public async Task<DepotFundModel> GetOrCreateByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.DepotFunds
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

        await _dbContext.DepotFunds.AddAsync(newFund, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Reload with Depot include
        var saved = await _dbContext.DepotFunds
            .Include(x => x.Depot)
            .FirstAsync(x => x.Id == newFund.Id, cancellationToken);

        return DepotFundMapper.ToModel(saved);
    }

    public async Task<List<DepotFundModel>> GetAllWithDepotInfoAsync(CancellationToken cancellationToken = default)
    {
        // LEFT JOIN: tất cả depot, kể cả chưa có fund record
        var results = await _dbContext.Depots
            .AsNoTracking()
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
        var entity = await _dbContext.DepotFunds
            .FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);

        if (entity != null)
        {
            DepotFundMapper.UpdateEntity(entity, model);
        }
    }

    public async Task CreateTransactionAsync(DepotFundTransactionModel transaction, CancellationToken cancellationToken = default)
    {
        var entity = DepotFundTransactionMapper.ToEntity(transaction);
        await _dbContext.DepotFundTransactions.AddAsync(entity, cancellationToken);
    }

    public async Task<Dictionary<int, decimal>> GetBalancesByDepotIdsAsync(IEnumerable<int> depotIds, CancellationToken cancellationToken = default)
    {
        var ids = depotIds.ToList();
        if (ids.Count == 0) return new Dictionary<int, decimal>();

        return await _dbContext.DepotFunds
            .Where(x => ids.Contains(x.DepotId))
            .AsNoTracking()
            .ToDictionaryAsync(x => x.DepotId, x => x.Balance, cancellationToken);
    }

    public async Task<PagedResult<DepotFundTransactionModel>> GetPagedTransactionsByDepotIdAsync(
        int depotId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DepotFundTransactions
            .Where(x => x.DepotFund.DepotId == depotId)
            .OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var models = items.Select(DepotFundTransactionMapper.ToModel).ToList();
        return new PagedResult<DepotFundTransactionModel>(models, totalCount, pageNumber, pageSize);
    }
}
