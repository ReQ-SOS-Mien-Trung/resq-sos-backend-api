using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Queries.GetCampaignFundFlowChart;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface IFundTransactionRepository
{
    Task<PagedResult<FundTransactionModel>> GetByCampaignIdAsync(
        int campaignId,
        int pageNumber,
        int pageSize,
        List<TransactionType>?          types          = null,
        List<TransactionDirection>?     directions     = null,
        List<TransactionReferenceType>? referenceTypes = null,
        DateOnly? fromDate = null,
        DateOnly? toDate   = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        CancellationToken cancellationToken = default);
    Task CreateAsync(FundTransactionModel transaction, CancellationToken cancellationToken = default);
    Task<bool> ExistsByReferenceAsync(
        TransactionReferenceType referenceType,
        int referenceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy dữ liệu biến động quỹ chiến dịch theo kỳ (In/Out) cho bar chart.
    /// </summary>
    Task<List<(DateTime Period, decimal TotalIn, decimal TotalOut)>> GetPeriodFundFlowAsync(
        int campaignId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default);
}
