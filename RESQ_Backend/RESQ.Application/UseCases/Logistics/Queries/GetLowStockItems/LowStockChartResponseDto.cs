namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

/// <summary>
/// Response wrapper gom summary + chart breakdowns + raw list - tối ưu cho frontend chart.
/// </summary>
public class LowStockChartResponseDto
{
    /// <summary>Tổng hợp toàn bộ vật phẩm cảnh báo.</summary>
    public LowStockSummaryDto Summary { get; set; } = new();

    /// <summary>Phân tích theo kho - dùng cho bar/column chart.</summary>
    public List<LowStockByDepotDto> ByDepot { get; set; } = new();

    /// <summary>Phân tích theo danh mục vật phẩm - dùng cho pie/donut chart.</summary>
    public List<LowStockByCategoryDto> ByCategory { get; set; } = new();

    /// <summary>Danh sách chi tiết từng vật phẩm - dùng cho data table.</summary>
    public List<LowStockItemDto> Items { get; set; } = new();
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalItemCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalItemCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

public class LowStockSummaryDto
{
    /// <summary>Số vật phẩm ở mức 🔴 CRITICAL.</summary>
    public int CriticalCount { get; set; }

    /// <summary>Số vật phẩm ở mức 🟠 MEDIUM.</summary>
    public int MediumCount { get; set; }

    /// <summary>Số vật phẩm ở mức 🟡 LOW.</summary>
    public int LowCount { get; set; }

    /// <summary>Số vật phẩm chưa được cấu hình threshold (UNCONFIGURED).</summary>
    public int UnconfiguredCount { get; set; }

    /// <summary>Tổng cộng (tất cả mức không phải OK).</summary>
    public int TotalCount { get; set; }
}

public class LowStockByDepotDto
{
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public int CriticalCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
}

public class LowStockByCategoryDto
{
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int CriticalCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
}

/// <summary>Helper tổng hợp flat list thành chart response.</summary>
internal static class LowStockChartBuilder
{
    internal static LowStockChartResponseDto Build(List<LowStockItemDto> items, int pageNumber, int pageSize)
    {
        var safePageNumber = pageNumber <= 0 ? 1 : pageNumber;
        var safePageSize = pageSize <= 0 ? 20 : pageSize;
        var pagedItems = items
            .Skip((safePageNumber - 1) * safePageSize)
            .Take(safePageSize)
            .ToList();

        return new LowStockChartResponseDto
        {
            Summary = new LowStockSummaryDto
            {
                CriticalCount    = items.Count(x => x.WarningLevel == "CRITICAL"),
                MediumCount      = items.Count(x => x.WarningLevel == "MEDIUM"),
                LowCount         = items.Count(x => x.WarningLevel == "LOW"),
                UnconfiguredCount = items.Count(x => x.WarningLevel == "UNCONFIGURED"),
                TotalCount       = items.Count
            },
            ByDepot = items
                .Where(x => x.WarningLevel is "CRITICAL" or "MEDIUM" or "LOW")
                .GroupBy(x => new { x.DepotId, x.DepotName })
                .Select(g => new LowStockByDepotDto
                {
                    DepotId      = g.Key.DepotId,
                    DepotName    = g.Key.DepotName,
                    CriticalCount = g.Count(x => x.WarningLevel == "CRITICAL"),
                    MediumCount  = g.Count(x => x.WarningLevel == "MEDIUM"),
                    LowCount     = g.Count(x => x.WarningLevel == "LOW")
                })
                .OrderBy(x => x.DepotId)
                .ToList(),
            ByCategory = items
                .Where(x => x.WarningLevel is "CRITICAL" or "MEDIUM" or "LOW")
                .GroupBy(x => new { x.CategoryId, x.CategoryName })
                .Select(g => new LowStockByCategoryDto
                {
                    CategoryId   = g.Key.CategoryId,
                    CategoryName = g.Key.CategoryName,
                    CriticalCount = g.Count(x => x.WarningLevel == "CRITICAL"),
                    MediumCount  = g.Count(x => x.WarningLevel == "MEDIUM"),
                    LowCount     = g.Count(x => x.WarningLevel == "LOW")
                })
                .OrderBy(x => x.CategoryId)
                .ToList(),
            Items = pagedItems,
            PageNumber = safePageNumber,
            PageSize = safePageSize,
            TotalItemCount = items.Count
        };
    }
}

