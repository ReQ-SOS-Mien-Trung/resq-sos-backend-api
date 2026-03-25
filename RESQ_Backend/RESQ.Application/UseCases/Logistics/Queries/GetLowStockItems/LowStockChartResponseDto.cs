namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

/// <summary>
/// Response wrapper gom summary + chart breakdowns + raw list — tối ưu cho frontend chart.
/// </summary>
public class LowStockChartResponseDto
{
    /// <summary>Tổng hợp toàn bộ vật tư cảnh báo.</summary>
    public LowStockSummaryDto Summary { get; set; } = new();

    /// <summary>Phân tích theo kho — dùng cho bar/column chart.</summary>
    public List<LowStockByDepotDto> ByDepot { get; set; } = new();

    /// <summary>Phân tích theo danh mục vật tư — dùng cho pie/donut chart.</summary>
    public List<LowStockByCategoryDto> ByCategory { get; set; } = new();

    /// <summary>Danh sách chi tiết từng vật tư — dùng cho data table.</summary>
    public List<LowStockItemDto> Items { get; set; } = new();
}

public class LowStockSummaryDto
{
    /// <summary>Số vật tư ở mức 🔴 Danger theo threshold đã resolve.</summary>
    public int DangerCount { get; set; }

    /// <summary>Số vật tư ở mức 🟡 Warning theo threshold đã resolve.</summary>
    public int WarningCount { get; set; }

    /// <summary>Tổng cộng (Danger + Warning).</summary>
    public int TotalCount { get; set; }
}

public class LowStockByDepotDto
{
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public int DangerCount { get; set; }
    public int WarningCount { get; set; }
}

public class LowStockByCategoryDto
{
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int DangerCount { get; set; }
    public int WarningCount { get; set; }
}

/// <summary>Helper tổng hợp flat list thành chart response.</summary>
internal static class LowStockChartBuilder
{
    internal static LowStockChartResponseDto Build(List<LowStockItemDto> items)
    {
        return new LowStockChartResponseDto
        {
            Summary = new LowStockSummaryDto
            {
                DangerCount  = items.Count(x => x.AlertLevel == "Danger"),
                WarningCount = items.Count(x => x.AlertLevel == "Warning"),
                TotalCount   = items.Count
            },
            ByDepot = items
                .GroupBy(x => new { x.DepotId, x.DepotName })
                .Select(g => new LowStockByDepotDto
                {
                    DepotId      = g.Key.DepotId,
                    DepotName    = g.Key.DepotName,
                    DangerCount  = g.Count(x => x.AlertLevel == "Danger"),
                    WarningCount = g.Count(x => x.AlertLevel == "Warning")
                })
                .OrderBy(x => x.DepotId)
                .ToList(),
            ByCategory = items
                .GroupBy(x => new { x.CategoryId, x.CategoryName })
                .Select(g => new LowStockByCategoryDto
                {
                    CategoryId   = g.Key.CategoryId,
                    CategoryName = g.Key.CategoryName,
                    DangerCount  = g.Count(x => x.AlertLevel == "Danger"),
                    WarningCount = g.Count(x => x.AlertLevel == "Warning")
                })
                .OrderBy(x => x.CategoryId)
                .ToList(),
            Items = items
        };
    }
}
