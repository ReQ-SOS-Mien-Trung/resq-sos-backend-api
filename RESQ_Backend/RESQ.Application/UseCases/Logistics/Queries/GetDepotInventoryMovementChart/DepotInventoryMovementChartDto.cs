namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryMovementChart;

/// <summary>
/// Một điểm dữ liệu trên biểu đồ biến động kho (line chart).
/// </summary>
public class InventoryMovementDataPoint
{
    /// <summary>Ngày (yyyy-MM-dd).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Tổng số lượng nhập kho trong ngày (Import, TransferIn, Return).</summary>
    public int TotalIn { get; set; }

    /// <summary>Tổng số lượng xuất kho trong ngày (Export, TransferOut).</summary>
    public int TotalOut { get; set; }

    /// <summary>Điều chỉnh tồn kho trong ngày (Adjust – có thể âm).</summary>
    public int TotalAdjust { get; set; }
}

/// <summary>
/// Response biểu đồ biến động kho (in/out) theo ngày – dùng cho line chart.
/// </summary>
public class DepotInventoryMovementChartDto
{
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<InventoryMovementDataPoint> DataPoints { get; set; } = [];
}
