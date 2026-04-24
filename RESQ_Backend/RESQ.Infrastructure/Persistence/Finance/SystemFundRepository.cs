using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Mappers.Finance;

namespace RESQ.Infrastructure.Persistence.Finance;

public class SystemFundRepository : ISystemFundRepository
{
    private readonly IUnitOfWork _unitOfWork;

    public SystemFundRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SystemFundModel> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.SetTracked<SystemFund>()
            .FirstOrDefaultAsync(cancellationToken);

        if (entity != null)
            return SystemFundMapper.ToModel(entity);

        // Tạo singleton quỹ hệ thống
        var newFund = new SystemFund
        {
            Name = "Quỹ hệ thống",
            Balance = 0m,
            LastUpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.GetRepository<SystemFund>().AddAsync(newFund);
        return SystemFundMapper.ToModel(newFund);
    }

    public async Task UpdateAsync(SystemFundModel model, CancellationToken cancellationToken = default)
    {
        var entity = new SystemFund
        {
            Id = model.Id,
            Name = model.Name,
            Balance = model.Balance,
            LastUpdatedAt = model.LastUpdatedAt,
            RowVersion = model.RowVersion
        };

        await _unitOfWork.GetRepository<SystemFund>().UpdateAsync(entity);
    }

    public async Task CreateTransactionAsync(SystemFundTransactionModel transaction, CancellationToken cancellationToken = default)
    {
        var entity = SystemFundMapper.ToTransactionEntity(transaction);
        await _unitOfWork.GetRepository<SystemFundTransaction>().AddAsync(entity);
    }

    public async Task<PagedResult<SystemFundTransactionModel>> GetPagedTransactionsAsync(
        int pageNumber,
        int pageSize,
        DateOnly? fromDate   = null,
        DateOnly? toDate     = null,
        decimal? minAmount   = null,
        decimal? maxAmount   = null,
        IReadOnlyCollection<SystemFundTransactionType>? transactionTypes = null,
        string? search       = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<SystemFundTransaction>().AsQueryable();

        if (transactionTypes is { Count: > 0 })
        {
            var typeNames = transactionTypes.Select(t => t.ToString()).ToList();
            query = query.Where(x => typeNames.Contains(x.TransactionType));
        }

        if (fromDate.HasValue)
            query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) <= toDate.Value);
        if (minAmount.HasValue)
            query = query.Where(x => x.Amount >= minAmount.Value);
        if (maxAmount.HasValue)
            query = query.Where(x => x.Amount <= maxAmount.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x =>
                (x.Note != null && x.Note.ToLower().Contains(s))
                || (x.ReferenceType != null && x.ReferenceType.ToLower().Contains(s))
            );
        }

        query = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var models = items.Select(SystemFundMapper.ToTransactionModel).ToList();
        return new PagedResult<SystemFundTransactionModel>(models, totalCount, pageNumber, pageSize);
    }
}
