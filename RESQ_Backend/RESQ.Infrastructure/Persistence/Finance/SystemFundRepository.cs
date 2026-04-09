using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
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
        await _unitOfWork.SaveAsync();

        return SystemFundMapper.ToModel(newFund);
    }

    public async Task UpdateAsync(SystemFundModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.SetTracked<SystemFund>()
            .FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);

        if (entity != null)
        {
            SystemFundMapper.UpdateEntity(entity, model);
        }
    }

    public async Task CreateTransactionAsync(SystemFundTransactionModel transaction, CancellationToken cancellationToken = default)
    {
        var entity = SystemFundMapper.ToTransactionEntity(transaction);
        await _unitOfWork.GetRepository<SystemFundTransaction>().AddAsync(entity);
    }

    public async Task<PagedResult<SystemFundTransactionModel>> GetPagedTransactionsAsync(
        int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<SystemFundTransaction>()
            .OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var models = items.Select(SystemFundMapper.ToTransactionModel).ToList();
        return new PagedResult<SystemFundTransactionModel>(models, totalCount, pageNumber, pageSize);
    }
}
