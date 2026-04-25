namespace RESQ.Application.UseCases.Finance.Queries.GetDepotFundMultiLineChart;

/// <summary>
/// Một điểm dữ liệu trên đường line của một quỹ kho cụ thể.
/// </summary>
public class FundLineDataPoint
{
    /// <summary>Ngày (yyyy-MM-dd).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Tổng tiền vào quỹ trong ngày.</summary>
    public decimal TotalIn { get; set; }

    /// <summary>Tổng tiền ra khỏi quỹ trong ngày.</summary>
    public decimal TotalOut { get; set; }
}

/// <summary>
/// Dữ liệu một đường line (một quỹ kho cụ thể).
/// </summary>
public class DepotFundLineSeries
{
    /// <summary>ID của quỹ kho (depot_fund.id).</summary>
    public int FundId { get; set; }

    /// <summary>Tên nguồn quỹ (VD: "Quỹ chiến dịch ABC", "Quỹ hệ thống").</summary>
    public string FundSourceName { get; set; } = string.Empty;

    /// <summary>Số dư hiện tại của quỹ.</summary>
    public decimal CurrentBalance { get; set; }

    /// <summary>Danh sách điểm dữ liệu theo ngày.</summary>
    public List<FundLineDataPoint> DataPoints { get; set; } = [];
}

/// <summary>
/// Response biểu đồ biến động quỹ kho multi-line (mỗi quỹ = một đường line).
/// </summary>
public class DepotFundMultiLineChartDto
{
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    /// <summary>Mỗi phần tử là một đường line tương ứng với một quỹ kho.</summary>
    public List<DepotFundLineSeries> Series { get; set; } = [];
}
