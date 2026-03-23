using MediatR;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

/// <param name="DepotId">Lọc theo kho cụ thể. Null = tất cả kho (dành cho Admin).</param>
/// <param name="AlertLevel">Lọc theo mức cảnh báo. Null = cả Warning lẫn Danger.</param>
public record GetLowStockItemsQuery(
    int? DepotId,
    StockAlertLevel? AlertLevel
) : IRequest<LowStockChartResponseDto>;
