using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
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
        var entity = await _unitOfWork.Set<DepotFund>()
            .Include(x => x.Depot)
            .FirstOrDefaultAsync(x => x.DepotId == depotId, cancellationToken);

        return entity == null ? null : DepotFundMapper.ToModel(entity);
    }

    public async Task<DepotFundModel> GetOrCreateByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.SetTracked<DepotFund>()
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
        var saved = await _unitOfWork.SetTracked<DepotFund>()
            .Include(x => x.Depot)
            .FirstAsync(x => x.Id == newFund.Id, cancellationToken);

        return DepotFundMapper.ToModel(saved);
    }

    public async Task<DepotFundModel?> GetByIdAsync(int depotFundId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.Set<DepotFund>()
            .Include(x => x.Depot)
            .FirstOrDefaultAsync(x => x.Id == depotFundId, cancellationToken);

        return entity == null ? null : DepotFundMapper.ToModel(entity);
    }

    public async Task<DepotFundModel> GetOrCreateByDepotAndSourceAsync(
        int depotId, FundSourceType sourceType, int? sourceId,
        CancellationToken cancellationToken = default)
    {
        var sourceTypeStr = sourceType.ToString();

        var entity = await _unitOfWork.SetTracked<DepotFund>()
            .Include(x => x.Depot)
            .FirstOrDefaultAsync(x => x.DepotId == depotId
                                   && x.FundSourceType == sourceTypeStr
                                   && x.FundSourceId == sourceId, cancellationToken);

        if (entity != null)
            return DepotFundMapper.ToModel(entity);

        // Tạo mới depot fund gắn với nguồn cụ thể
        var newFund = new DepotFund
        {
            DepotId = depotId,
            Balance = 0m,
            AdvanceLimit = 0m,
            LastUpdatedAt = DateTime.UtcNow,
            FundSourceType = sourceTypeStr,
            FundSourceId = sourceId
        };

        await _unitOfWork.GetRepository<DepotFund>().AddAsync(newFund);
        await _unitOfWork.SaveAsync();

        var saved = await _unitOfWork.SetTracked<DepotFund>()
            .Include(x => x.Depot)
            .FirstAsync(x => x.Id == newFund.Id, cancellationToken);

        return DepotFundMapper.ToModel(saved);
    }

    public async Task<List<DepotFundModel>> GetAllByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.Set<DepotFund>()
            .Include(x => x.Depot)
            .Where(x => x.DepotId == depotId)
            .OrderByDescending(x => x.LastUpdatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(DepotFundMapper.ToModel).ToList();
    }

    public async Task<List<DepotFundModel>> GetAllWithDepotInfoAsync(CancellationToken cancellationToken = default)
    {
        // Lấy tất cả depot funds kèm depot info
        var funds = await _unitOfWork.Set<DepotFund>()
            .Include(x => x.Depot)
            .OrderBy(x => x.DepotId)
            .ThenByDescending(x => x.LastUpdatedAt)
            .ToListAsync(cancellationToken);

        return funds.Select(DepotFundMapper.ToModel).ToList();
    }

    public async Task UpdateAsync(DepotFundModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.SetTracked<DepotFund>()
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

        return await _unitOfWork.Set<DepotFund>()
            .Where(x => ids.Contains(x.DepotId))
            .ToDictionaryAsync(x => x.DepotId, x => x.Balance, cancellationToken);
    }

    public async Task<PagedResult<DepotFundTransactionModel>> GetPagedTransactionsByDepotIdAsync(
        int depotId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<DepotFundTransaction>()
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

    public async Task<PagedResult<DepotFundTransactionModel>> GetPagedTransactionsByFundIdAsync(
        int depotFundId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<DepotFundTransaction>()
            .Where(x => x.DepotFundId == depotFundId)
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
