using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotCapacityChart;

/// <summary>
/// Truy vấn dữ liệu biểu đồ tiến trình sức chứa kho (thể tích &amp; cân nặng).
/// </summary>
public record GetDepotCapacityChartQuery(int DepotId) : IRequest<DepotCapacityChartDto>;
