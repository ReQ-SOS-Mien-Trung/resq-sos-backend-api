namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotCapacityChart;

/// <summary>
/// Dữ liệu biểu đồ tiến trình sức chứa kho – dùng cho progress chart.
/// </summary>
public class DepotCapacityChartDto
{
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;

    // Thể tích
    /// <summary>Thể tích hiện tại đang sử dụng (dm³).</summary>
    public decimal CurrentVolume { get; set; }
    /// <summary>Sức chứa tối đa theo thể tích (dm³).</summary>
    public decimal MaxVolume { get; set; }
    /// <summary>Phần trăm sử dụng thể tích (0–100).</summary>
    public decimal VolumeUsagePercent { get; set; }

    // Cân nặng
    /// <summary>Cân nặng hiện tại đang sử dụng (kg).</summary>
    public decimal CurrentWeight { get; set; }
    /// <summary>Sức chứa tối đa theo cân nặng (kg).</summary>
    public decimal MaxWeight { get; set; }
    /// <summary>Phần trăm sử dụng cân nặng (0–100).</summary>
    public decimal WeightUsagePercent { get; set; }
}
