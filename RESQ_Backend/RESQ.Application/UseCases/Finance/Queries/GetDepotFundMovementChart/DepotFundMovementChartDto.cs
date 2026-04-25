namespace RESQ.Application.UseCases.Finance.Queries.GetDepotFundMovementChart;

/// <summary>
/// Một điểm dữ liệu trên biểu đồ biến động quỹ kho (point styling line chart).
/// </summary>
public class FundMovementDataPoint
{
    /// <summary>Ngày (yyyy-MM-dd).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Tổng tiền vào quỹ kho trong ngày (Allocation, Refund, LiquidationRevenue…).</summary>
    public decimal TotalIn { get; set; }

    /// <summary>Tổng tiền ra khỏi quỹ kho trong ngày (Deduction, ClosureFundReturn, PersonalAdvance…).</summary>
    public decimal TotalOut { get; set; }
}

/// <summary>
/// Response biểu đồ biến động quỹ kho (in/out) theo ngày – dùng cho point styling line chart.
/// </summary>
public class DepotFundMovementChartDto
{
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<FundMovementDataPoint> DataPoints { get; set; } = [];
}

/// <summary>
/// Dữ liệu biến động một quỹ kho theo ngày (dùng cho multi-line chart).
/// </summary>
public class PerFundMovementSeries
{
    public int FundId { get; set; }
    public string FundSourceName { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public List<FundMovementDataPoint> DataPoints { get; set; } = [];
}
