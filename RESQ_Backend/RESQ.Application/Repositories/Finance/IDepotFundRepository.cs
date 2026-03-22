using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Repositories.Finance;

public interface IDepotFundRepository
{
    /// <summary>Lấy quỹ kho theo depot ID.</summary>
    Task<DepotFundModel?> GetByDepotIdAsync(int depotId, CancellationToken cancellationToken = default);

    /// <summary>Lấy hoặc tạo mới quỹ kho (lazy init — balance = 0 nếu chưa có).</summary>
    Task<DepotFundModel> GetOrCreateByDepotIdAsync(int depotId, CancellationToken cancellationToken = default);

    /// <summary>Lấy tất cả quỹ kho kèm thông tin depot.</summary>
    Task<List<DepotFundModel>> GetAllWithDepotInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>Cập nhật số dư quỹ kho (balance, last_updated_at).</summary>
    Task UpdateAsync(DepotFundModel model, CancellationToken cancellationToken = default);

    /// <summary>Tạo giao dịch quỹ kho (audit trail).</summary>
    Task CreateTransactionAsync(DepotFundTransactionModel transaction, CancellationToken cancellationToken = default);

    /// <summary>Lấy số dư quỹ cho nhiều depot cùng lúc (dùng cho spending endpoint).</summary>
    Task<Dictionary<int, decimal>> GetBalancesByDepotIdsAsync(IEnumerable<int> depotIds, CancellationToken cancellationToken = default);

    /// <summary>Lấy lịch sử giao dịch quỹ của một kho theo depot ID (có phân trang, sắp xếp mới nhất trước).</summary>
    Task<PagedResult<DepotFundTransactionModel>> GetPagedTransactionsByDepotIdAsync(int depotId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
