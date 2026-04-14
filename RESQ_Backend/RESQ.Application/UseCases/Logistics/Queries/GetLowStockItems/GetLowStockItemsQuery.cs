using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

/// <param name="DepotId">L?c theo kho c? th?. Null = t?t c? kho (dành cho Admin).</param>
/// <param name="WarningLevel">L?c theo level c? th? (CRITICAL/MEDIUM/LOW/UNCONFIGURED). Null = t?t c? m?c không ph?i OK.</param>
/// <param name="IncludeUnconfigured">Có bao g?m v?t ph?m chua c?u hình threshold không (UNCONFIGURED).</param>
public record GetLowStockItemsQuery(
    int? DepotId,
    string? WarningLevel = null,
    bool IncludeUnconfigured = false
) : IRequest<LowStockChartResponseDto>;

