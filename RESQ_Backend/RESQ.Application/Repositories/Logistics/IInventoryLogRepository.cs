using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryMovementChart;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryTransactionHistory;
using RESQ.Domain.Entities.Logistics.Models;

namespace RESQ.Application.Repositories.Logistics;

public interface IInventoryLogRepository
{
    Task<PagedResult<InventoryLogModel>> GetInventoryLogsPagedAsync(
        int? depotId,
        int? itemModelId,
        List<string>? actionTypes,
        List<string>? sourceTypes,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? search = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<PagedResult<InventoryTransactionDto>> GetTransactionHistoryAsync(
        int? depotId,
        int? itemModelId,
        List<string>? actionTypes,
        List<string>? sourceTypes,
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy dữ liệu biến động kho theo ngày (in/out/adjust) cho line chart.
    /// </summary>
    Task<List<InventoryMovementDataPoint>> GetDailyMovementChartAsync(
        int depotId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default);
}
