using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface IDepotFundRepository
{
    Task<DepotFundModel?> GetByDepotIdAsync(int depotId, CancellationToken cancellationToken = default);
    Task<DepotFundModel> GetOrCreateByDepotIdAsync(int depotId, CancellationToken cancellationToken = default);
    Task<DepotFundModel?> GetByIdAsync(int depotFundId, CancellationToken cancellationToken = default);
    Task<List<DepotFundModel>> GetByIdsAsync(IEnumerable<int> depotFundIds, CancellationToken cancellationToken = default);

    Task<DepotFundModel> GetOrCreateByDepotAndSourceAsync(
        int depotId,
        FundSourceType sourceType,
        int? sourceId,
        CancellationToken cancellationToken = default);

    Task<List<DepotFundModel>> GetAllByDepotIdAsync(int depotId, CancellationToken cancellationToken = default);
    Task<List<DepotFundModel>> GetAllWithDepotInfoAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(DepotFundModel model, CancellationToken cancellationToken = default);
    Task CreateTransactionAsync(DepotFundTransactionModel transaction, CancellationToken cancellationToken = default);
    Task<Dictionary<int, decimal>> GetBalancesByDepotIdsAsync(IEnumerable<int> depotIds, CancellationToken cancellationToken = default);

    Task<PagedResult<DepotFundTransactionModel>> GetPagedTransactionsByDepotIdAsync(
        int depotId,
        int pageNumber,
        int pageSize,
        IReadOnlyCollection<DepotFundTransactionType>? transactionTypes = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<DepotFundTransactionModel>> GetPagedTransactionsByFundIdAsync(
        int depotFundId,
        int pageNumber,
        int pageSize,
        IReadOnlyCollection<DepotFundTransactionType>? transactionTypes = null,
        CancellationToken cancellationToken = default);

    Task<List<ContributorDebtModel>> GetContributorDebtsByDepotAsync(
        int depotId,
        IEnumerable<ContributorDebtModel> contributors,
        CancellationToken cancellationToken = default);

    Task<List<ContributorDebtByFundModel>> GetContributorDebtsByFundAsync(
        int depotId,
        IEnumerable<int> depotFundIds,
        IEnumerable<ContributorDebtModel> contributors,
        CancellationToken cancellationToken = default);
}
