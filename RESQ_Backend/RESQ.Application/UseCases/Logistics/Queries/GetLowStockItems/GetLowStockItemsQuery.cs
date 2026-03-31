using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

/// <param name="DepotId">Lọc theo kho cụ thể. Null = tất cả kho (dành cho Admin).</param>
/// <param name="WarningLevel">Lọc theo level cụ thể (CRITICAL/MEDIUM/LOW/UNCONFIGURED). Null = tất cả mức không phải OK.</param>
/// <param name="IncludeUnconfigured">Có bao gồm vật tư chưa cấu hình threshold không (UNCONFIGURED).</param>
public record GetLowStockItemsQuery(
    int? DepotId,
    string? WarningLevel = null,
    bool IncludeUnconfigured = false
) : IRequest<LowStockChartResponseDto>;

