using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface ISystemFundRepository
{
    /// <summary>Lấy quỹ hệ thống (singleton). Tạo mới nếu chưa có.</summary>
    Task<SystemFundModel> GetOrCreateAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(SystemFundModel model, CancellationToken cancellationToken = default);

    Task CreateTransactionAsync(SystemFundTransactionModel transaction, CancellationToken cancellationToken = default);

    /// <summary>Lấy lịch sử giao dịch quỹ hệ thống (phân trang).</summary>
    Task<PagedResult<SystemFundTransactionModel>> GetPagedTransactionsAsync(
        int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
