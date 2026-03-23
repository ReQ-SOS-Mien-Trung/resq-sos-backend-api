using MediatR;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

/// <param name="UserId">Manager user ID — depot sẽ được tự động resolve.</param>
/// <param name="AlertLevel">Lọc theo mức cảnh báo. Null = cả Warning lẫn Danger.</param>
public record GetMyDepotLowStockQuery(
    Guid UserId,
    StockAlertLevel? AlertLevel
) : IRequest<LowStockChartResponseDto>;
