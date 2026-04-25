using MediatR;

namespace RESQ.Application.UseCases.Finance.Queries.GetDepotFundMultiLineChart;

/// <summary>
/// Truy vấn biểu đồ biến động quỹ kho theo từng quỹ (multi-line chart).
/// Mỗi quỹ kho (fund source) trong kho sẽ tương ứng một đường line.
/// </summary>
public class GetDepotFundMultiLineChartQuery : IRequest<DepotFundMultiLineChartDto>
{
    public int DepotId { get; set; }

    /// <summary>Thời điểm bắt đầu (UTC). Không truyền = lấy từ đầu.</summary>
    public DateTime? From { get; set; }

    /// <summary>Thời điểm kết thúc (UTC). Không truyền = hiện tại.</summary>
    public DateTime? To { get; set; }
}
