using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

/// <param name="UserId">Manager user ID - depot sẽ được tự động resolve.</param>
/// <param name="WarningLevel">Lọc theo level cụ thể (CRITICAL/MEDIUM/LOW/UNCONFIGURED). Null = tất cả mức không phải OK.</param>
/// <param name="IncludeUnconfigured">Có bao gồm vật tư chưa cấu hình threshold không (UNCONFIGURED).</param>
public record GetMyDepotLowStockQuery(
    Guid UserId,
    string? WarningLevel = null,
    bool IncludeUnconfigured = false
) : IRequest<LowStockChartResponseDto>;

