using MediatR;

namespace RESQ.Application.UseCases.Finance.Queries.GetDepotFundMovementChart;

/// <summary>
/// Truy vấn biểu đồ biến động quỹ kho (in/out theo ngày) – point styling line chart.
/// Nếu chỉ truyền ngày (không có giờ) thì From mặc định 00:00:00, To mặc định 23:59:59.
/// Nếu không truyền gì thì lấy toàn bộ dữ liệu.
/// </summary>
public class GetDepotFundMovementChartQuery : IRequest<DepotFundMovementChartDto>
{
    public int DepotId { get; set; }

    /// <summary>Thời điểm bắt đầu (UTC). Không truyền = lấy từ đầu.</summary>
    public DateTime? From { get; set; }

    /// <summary>Thời điểm kết thúc (UTC). Không truyền = hiện tại.</summary>
    public DateTime? To { get; set; }
}
