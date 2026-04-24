using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.UseCases.Finance.Queries.GetDepotFundMovementChart;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;
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
            .OrderByDescending(x => x.LastUpdatedAt)
            .FirstOrDefaultAsync(x => x.DepotId == depotId, cancellationToken);

        if (entity == null) return null;

        var model = DepotFundMapper.ToModel(entity);
        await PopulateFundSourceNamesAsync([model], cancellationToken);
        return model;
    }

    public async Task<DepotFundModel> GetOrCreateByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.SetTracked<DepotFund>()
            .Include(x => x.Depot)
            .OrderByDescending(x => x.LastUpdatedAt)
            .FirstOrDefaultAsync(x => x.DepotId == depotId, cancellationToken);

        if (entity == null)
        {
            entity = new DepotFund
            {
                DepotId = depotId,
                Balance = 0m,
                LastUpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.GetRepository<DepotFund>().AddAsync(entity);
        }

        var model = DepotFundMapper.ToModel(entity);
        await PopulateFundSourceNamesAsync([model], cancellationToken);
        return model;
    }

    public async Task<DepotFundModel?> GetByIdAsync(int depotFundId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.Set<DepotFund>()
            .Include(x => x.Depot)
            .FirstOrDefaultAsync(x => x.Id == depotFundId, cancellationToken);

        if (entity == null) return null;

        var model = DepotFundMapper.ToModel(entity);
        await PopulateFundSourceNamesAsync([model], cancellationToken);
        return model;
    }

    public async Task<List<DepotFundModel>> GetByIdsAsync(IEnumerable<int> depotFundIds, CancellationToken cancellationToken = default)
    {
        var ids = depotFundIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var entities = await _unitOfWork.Set<DepotFund>()
            .Include(x => x.Depot)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var models = entities.Select(DepotFundMapper.ToModel).ToList();
        await PopulateFundSourceNamesAsync(models, cancellationToken);
        return models;
    }

    public async Task<DepotFundModel> GetOrCreateByDepotAndSourceAsync(
        int depotId,
        FundSourceType sourceType,
        int? sourceId,
        CancellationToken cancellationToken = default)
    {
        var sourceTypeStr = sourceType.ToString();

        var entity = await _unitOfWork.SetTracked<DepotFund>()
            .Include(x => x.Depot)
            .FirstOrDefaultAsync(x => x.DepotId == depotId
                                   && x.FundSourceType == sourceTypeStr
                                   && x.FundSourceId == sourceId, cancellationToken);

        if (entity == null)
        {
            entity = new DepotFund
            {
                DepotId = depotId,
                Balance = 0m,
                LastUpdatedAt = DateTime.UtcNow,
                FundSourceType = sourceTypeStr,
                FundSourceId = sourceId
            };

            await _unitOfWork.GetRepository<DepotFund>().AddAsync(entity);
        }

        var model = DepotFundMapper.ToModel(entity);
        await PopulateFundSourceNamesAsync([model], cancellationToken);
        return model;
    }

    public async Task<List<DepotFundModel>> GetAllByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.Set<DepotFund>()
            .Include(x => x.Depot)
            .Where(x => x.DepotId == depotId)
            .OrderByDescending(x => x.LastUpdatedAt)
            .ToListAsync(cancellationToken);

        var models = entities.Select(DepotFundMapper.ToModel).ToList();
        await PopulateFundSourceNamesAsync(models, cancellationToken);
        return models;
    }

    public async Task<List<DepotFundModel>> GetAllWithDepotInfoAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.Set<DepotFund>()
            .Include(x => x.Depot)
            .OrderBy(x => x.DepotId)
            .ThenByDescending(x => x.LastUpdatedAt)
            .ToListAsync(cancellationToken);

        var models = entities.Select(DepotFundMapper.ToModel).ToList();
        await PopulateFundSourceNamesAsync(models, cancellationToken);
        return models;
    }

    public async Task UpdateAsync(DepotFundModel model, CancellationToken cancellationToken = default)
    {
        var entity = DepotFundMapper.ToEntity(model);
        await _unitOfWork.GetRepository<DepotFund>().UpdateAsync(entity);
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
            .GroupBy(x => x.DepotId)
            .Select(g => new { DepotId = g.Key, Balance = g.Sum(x => x.Balance) })
            .ToDictionaryAsync(x => x.DepotId, x => x.Balance, cancellationToken);
    }

    public async Task<PagedResult<DepotFundTransactionModel>> GetPagedTransactionsByDepotIdAsync(
        int depotId,
        int pageNumber,
        int pageSize,
        IReadOnlyCollection<DepotFundTransactionType>? transactionTypes = null,
        DateOnly? fromDate = null,
        DateOnly? toDate   = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        IReadOnlyCollection<DepotFundReferenceType>? referenceTypes = null,
        string? search     = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<DepotFundTransaction>()
            .Where(x => x.DepotFund.DepotId == depotId);

        if (transactionTypes is { Count: > 0 })
        {
            var typeNames = DepotFundTransactionTypeAlias.Expand(transactionTypes);
            query = query.Where(x => typeNames.Contains(x.TransactionType));
        }

        if (referenceTypes is { Count: > 0 })
        {
            var refNames = referenceTypes.Select(r => r.ToString()).ToList();
            query = query.Where(x => x.ReferenceType != null && refNames.Contains(x.ReferenceType));
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
                || (x.ContributorName != null && x.ContributorName.ToLower().Contains(s))
                || (x.ContributorPhoneNumber != null && x.ContributorPhoneNumber.ToLower().Contains(s))
            );
        }

        query = query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id);

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
        IReadOnlyCollection<DepotFundTransactionType>? transactionTypes = null,
        DateOnly? fromDate = null,
        DateOnly? toDate   = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        IReadOnlyCollection<DepotFundReferenceType>? referenceTypes = null,
        string? search     = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<DepotFundTransaction>()
            .Where(x => x.DepotFundId == depotFundId);

        if (transactionTypes is { Count: > 0 })
        {
            var typeNames = DepotFundTransactionTypeAlias.Expand(transactionTypes);
            query = query.Where(x => typeNames.Contains(x.TransactionType));
        }

        if (referenceTypes is { Count: > 0 })
        {
            var refNames = referenceTypes.Select(r => r.ToString()).ToList();
            query = query.Where(x => x.ReferenceType != null && refNames.Contains(x.ReferenceType));
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
                || (x.ContributorName != null && x.ContributorName.ToLower().Contains(s))
                || (x.ContributorPhoneNumber != null && x.ContributorPhoneNumber.ToLower().Contains(s))
            );
        }

        query = query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var models = items.Select(DepotFundTransactionMapper.ToModel).ToList();
        return new PagedResult<DepotFundTransactionModel>(models, totalCount, pageNumber, pageSize);
    }

    
    public async Task<PagedResult<ContributorDebtModel>> GetPagedAdvancersByDepotIdAsync(
        int depotId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var personalAdvanceTypes = DepotFundTransactionTypeAlias.GetNames(DepotFundTransactionType.PersonalAdvance);
        var repaymentTypes = DepotFundTransactionTypeAlias.GetNames(DepotFundTransactionType.AdvanceRepayment);

        var query = _unitOfWork.Set<DepotFundTransaction>()
            .Where(x => x.DepotFund.DepotId == depotId
                        && x.ContributorName != null
                        && x.ContributorPhoneNumber != null
                        && (personalAdvanceTypes.Contains(x.TransactionType) || repaymentTypes.Contains(x.TransactionType)))
            .GroupBy(x => new
            {
                x.ContributorName,
                x.ContributorPhoneNumber
            })
            .Select(g => new ContributorDebtModel
            {
                ContributorName = g.Key.ContributorName!,
                ContributorPhoneNumber = g.Key.ContributorPhoneNumber!,
                TotalAdvancedAmount = g.Sum(x => personalAdvanceTypes.Contains(x.TransactionType) ? x.Amount : 0m),
                TotalRepaidAmount = g.Sum(x => repaymentTypes.Contains(x.TransactionType) ? x.Amount : 0m)
            })
            .Where(x => x.TotalAdvancedAmount > 0)
            .OrderByDescending(x => x.TotalAdvancedAmount - x.TotalRepaidAmount);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ContributorDebtModel>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<List<ContributorDebtModel>> GetContributorDebtsByDepotAsync(
        int depotId,
        IEnumerable<ContributorDebtModel> contributors,
        CancellationToken cancellationToken = default)
    {
        var contributorList = contributors
            .Where(x => !string.IsNullOrWhiteSpace(x.ContributorName) && !string.IsNullOrWhiteSpace(x.ContributorPhoneNumber))
            .ToList();

        if (contributorList.Count == 0) return [];

        var lowerNames = contributorList.Select(x => x.ContributorName.ToLowerInvariant()).Distinct().ToList();
        var phones = contributorList.Select(x => x.ContributorPhoneNumber).Distinct().ToList();

        var personalAdvanceTypes = DepotFundTransactionTypeAlias.GetNames(DepotFundTransactionType.PersonalAdvance);
        var repaymentTypes = DepotFundTransactionTypeAlias.GetNames(DepotFundTransactionType.AdvanceRepayment);

        return await _unitOfWork.Set<DepotFundTransaction>()
            .Where(x => x.DepotFund.DepotId == depotId
                        && x.ContributorName != null
                        && x.ContributorPhoneNumber != null
                        && lowerNames.Contains(x.ContributorName.ToLower())
                        && phones.Contains(x.ContributorPhoneNumber)
                        && (personalAdvanceTypes.Contains(x.TransactionType) || repaymentTypes.Contains(x.TransactionType)))
            .GroupBy(x => new
            {
                x.ContributorName,
                x.ContributorPhoneNumber
            })
            .Select(g => new ContributorDebtModel
            {
                ContributorName = g.Key.ContributorName!,
                ContributorPhoneNumber = g.Key.ContributorPhoneNumber!,
                TotalAdvancedAmount = g.Sum(x => personalAdvanceTypes.Contains(x.TransactionType) ? x.Amount : 0m),
                TotalRepaidAmount = g.Sum(x => repaymentTypes.Contains(x.TransactionType) ? x.Amount : 0m)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ContributorDebtByFundModel>> GetContributorDebtsByFundAsync(
        int depotId,
        IEnumerable<int> depotFundIds,
        IEnumerable<ContributorDebtModel> contributors,
        CancellationToken cancellationToken = default)
    {
        var fundIds = depotFundIds.Distinct().ToList();
        if (fundIds.Count == 0) return [];

        var contributorList = contributors
            .Where(x => !string.IsNullOrWhiteSpace(x.ContributorName) && !string.IsNullOrWhiteSpace(x.ContributorPhoneNumber))
            .ToList();
        if (contributorList.Count == 0) return [];

        var lowerNames = contributorList.Select(x => x.ContributorName.ToLowerInvariant()).Distinct().ToList();
        var phones = contributorList.Select(x => x.ContributorPhoneNumber).Distinct().ToList();

        var personalAdvanceTypes = DepotFundTransactionTypeAlias.GetNames(DepotFundTransactionType.PersonalAdvance);
        var repaymentTypes = DepotFundTransactionTypeAlias.GetNames(DepotFundTransactionType.AdvanceRepayment);

        return await _unitOfWork.Set<DepotFundTransaction>()
            .Where(x => x.DepotFund.DepotId == depotId
                        && fundIds.Contains(x.DepotFundId)
                        && x.ContributorName != null
                        && x.ContributorPhoneNumber != null
                        && lowerNames.Contains(x.ContributorName.ToLower())
                        && phones.Contains(x.ContributorPhoneNumber)
                        && (personalAdvanceTypes.Contains(x.TransactionType) || repaymentTypes.Contains(x.TransactionType)))
            .GroupBy(x => new
            {
                x.DepotFundId,
                x.ContributorName,
                x.ContributorPhoneNumber
            })
            .Select(g => new ContributorDebtByFundModel
            {
                DepotFundId = g.Key.DepotFundId,
                ContributorName = g.Key.ContributorName!,
                ContributorPhoneNumber = g.Key.ContributorPhoneNumber!,
                TotalAdvancedAmount = g.Sum(x => personalAdvanceTypes.Contains(x.TransactionType) ? x.Amount : 0m),
                TotalRepaidAmount = g.Sum(x => repaymentTypes.Contains(x.TransactionType) ? x.Amount : 0m)
            })
            .ToListAsync(cancellationToken);
    }

    private async Task PopulateFundSourceNamesAsync(List<DepotFundModel> funds, CancellationToken cancellationToken)
    {
        if (funds.Count == 0) return;

        var campaignIds = funds
            .Where(x => x.FundSourceType == FundSourceType.Campaign && x.FundSourceId.HasValue)
            .Select(x => x.FundSourceId!.Value)
            .Distinct()
            .ToList();

        var campaignNames = campaignIds.Count == 0
            ? new Dictionary<int, string>()
            : await _unitOfWork.Set<FundCampaign>()
                .Where(x => campaignIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name ?? $"Campaign #{x.Id}", cancellationToken);

        var systemFundName = await _unitOfWork.Set<SystemFund>()
            .OrderBy(x => x.Id)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Quỹ hệ thống";

        foreach (var fund in funds)
        {
            switch (fund.FundSourceType)
            {
                case FundSourceType.Campaign:
                    if (fund.FundSourceId.HasValue && campaignNames.TryGetValue(fund.FundSourceId.Value, out var campaignName))
                    {
                        fund.FundSourceName = campaignName;
                    }
                    else
                    {
                        fund.FundSourceName = fund.FundSourceId.HasValue ? $"Campaign #{fund.FundSourceId.Value}" : "Campaign";
                    }
                    break;
                case FundSourceType.SystemFund:
                    fund.FundSourceName = systemFundName;
                    break;
                default:
                    fund.FundSourceName = null;
                    break;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<List<FundMovementDataPoint>> GetDailyFundMovementChartAsync(
        int depotId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        // IN types:  Allocation, Refund, LiquidationRevenue, AdvanceRepayment, PersonalAdvance
        //            (PersonalAdvance = tiền ứng ngoài vào quỹ kho → cộng quỹ)
        // OUT types: Deduction (trừ quỹ khi mua hàng), ClosureFundReturn (hoàn về hệ thống)
        //
        // Always use TransactionType to determine direction – never rely on Amount sign
        // because most transactions store a positive Amount regardless of direction.
        static bool IsFundIn(string type) =>
            DepotFundTransactionTypeAlias.TryParse(type, out var t) &&
            t is DepotFundTransactionType.Allocation
              or DepotFundTransactionType.Refund
              or DepotFundTransactionType.LiquidationRevenue
              or DepotFundTransactionType.PersonalAdvance
              or DepotFundTransactionType.AdvanceRepayment;

        static bool IsFundOut(string type) =>
            DepotFundTransactionTypeAlias.TryParse(type, out var t) &&
            t is DepotFundTransactionType.Deduction
              or DepotFundTransactionType.ClosureFundReturn;

        // Build base query with depot filter
        var query = _unitOfWork.Set<DepotFundTransaction>()
            .Where(x => x.DepotFund.DepotId == depotId);

        if (fromUtc.HasValue)
            query = query.Where(x => x.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(x => x.CreatedAt <= toUtc.Value);

        // Pull CreatedAt as DateTime, convert to DateOnly in memory (safe Npgsql translation)
        var rawRows = (await query
            .Select(x => new
            {
                x.CreatedAt,
                x.TransactionType,
                Amount = Math.Abs(x.Amount)
            })
            .ToListAsync(cancellationToken))
            .Select(x => new
            {
                Date            = DateOnly.FromDateTime(x.CreatedAt),
                x.TransactionType,
                x.Amount
            })
            .ToList();

        if (rawRows.Count == 0)
            return [];

        // Determine the date range from actual data when caller didn’t provide bounds
        var effectiveFrom = fromUtc.HasValue
            ? DateOnly.FromDateTime(fromUtc.Value)
            : rawRows.Min(x => x.Date);
        var effectiveTo = toUtc.HasValue
            ? DateOnly.FromDateTime(toUtc.Value)
            : rawRows.Max(x => x.Date);

        var grouped = rawRows.GroupBy(x => x.Date)
                             .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<FundMovementDataPoint>();
        for (var d = effectiveFrom; d <= effectiveTo; d = d.AddDays(1))
        {
            var point = new FundMovementDataPoint { Date = d };

            if (grouped.TryGetValue(d, out var rows))
            {
                foreach (var r in rows)
                {
                    if (IsFundIn(r.TransactionType))
                        point.TotalIn  += r.Amount;
                    else if (IsFundOut(r.TransactionType))
                        point.TotalOut += r.Amount;
                }
            }

            result.Add(point);
        }

        return result;
    }
}
