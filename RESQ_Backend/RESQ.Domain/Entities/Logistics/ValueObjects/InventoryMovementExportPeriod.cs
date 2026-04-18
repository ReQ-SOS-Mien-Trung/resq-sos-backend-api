using RESQ.Domain.Enum.Logistics;

namespace RESQ.Domain.Entities.Logistics.ValueObjects;

/// <summary>
/// Value object đóng gói khoảng thời gian xuất báo cáo biến động kho.
/// Hỗ trợ 2 chế độ: theo khoảng ngày tùy chọn (ByDateRange) và theo tháng/năm (ByMonth).
/// </summary>
public sealed class InventoryMovementExportPeriod
{
    public ExportPeriodType PeriodType { get; }
    public DateTime From { get; }
    public DateTime To { get; }
    public string DisplayTitle { get; }

    private InventoryMovementExportPeriod(
        ExportPeriodType periodType,
        DateTime from,
        DateTime to,
        string displayTitle)
    {
        PeriodType = periodType;
        From = from;
        To = to;
        DisplayTitle = displayTitle;
    }

    /// <summary>Xuất theo khoảng ngày tùy chọn (VD: 01/03/2026 → 15/03/2026).</summary>
    public static InventoryMovementExportPeriod ForDateRange(DateOnly fromDate, DateOnly toDate)
    {
        var from = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to   = toDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        return new InventoryMovementExportPeriod(
            ExportPeriodType.ByDateRange,
            from,
            to,
            $"Từ {fromDate:dd/MM/yyyy} đến {toDate:dd/MM/yyyy}");
    }

    /// <summary>Xuất theo một tháng cụ thể (VD: tháng 3/2026).</summary>
    public static InventoryMovementExportPeriod ForMonth(int year, int month)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1).AddTicks(-1);
        return new InventoryMovementExportPeriod(
            ExportPeriodType.ByMonth,
            from,
            to,
            $"Tháng {month:00}/{year}");
    }

    public string GetFileName(string depotName = "")
    {
        var safeName = string.IsNullOrWhiteSpace(depotName)
            ? "Toan-He-Thong"
            : depotName
                .Replace("/", "-").Replace("\\", "-")
                .Replace(":", "").Replace("*", "").Replace("?", "")
                .Replace("\"", "").Replace("<", "").Replace(">", "").Replace("|", "")
                .Trim();
        return $"Biến động Kho {safeName} {DisplayTitle}.xlsx";
    }
}
