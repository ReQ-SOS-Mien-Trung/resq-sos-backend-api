using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;

/// <summary>
/// Lấy dữ liệu biến động victim theo khoảng thời gian cho dashboard admin.
/// </summary>
/// <param name="From">Ngày bắt đầu (inclusive). Null → 6 tháng trước hôm nay.</param>
/// <param name="To">Ngày kết thúc (inclusive). Null → hôm nay.</param>
/// <param name="Granularity">"day" | "week" | "month" (không phân biệt hoa/thường). Null → "month".</param>
/// <param name="Statuses">Danh sách SOS status cần lọc. Null → lấy tất cả.</param>
public record GetVictimsByPeriodQuery(
    DateTime? From,
    DateTime? To,
    string? Granularity,
    List<string>? Statuses
) : IRequest<List<VictimsByPeriodDto>>;
