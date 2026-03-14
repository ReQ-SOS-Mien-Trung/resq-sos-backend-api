using RESQ.Domain.Enum.Logistics;

namespace RESQ.Domain.Entities.Logistics.ValueObjects;

/// <summary>
/// Value object đóng gói khoảng thời gian xuất báo cáo biến động kho.
/// Hỗ trợ 3 chế độ: theo tháng, theo năm, theo khoảng tháng.
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

    /// <summary>Xuất theo toàn bộ một năm (VD: năm 2025).</summary>
    public static InventoryMovementExportPeriod ForYear(int year)
    {
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(year, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc);
        return new InventoryMovementExportPeriod(
            ExportPeriodType.ByYear,
            from,
            to,
            $"Năm {year}");
    }

    /// <summary>Xuất theo khoảng từ tháng X đến tháng Y.</summary>
    public static InventoryMovementExportPeriod ForMonthRange(
        int fromYear, int fromMonth,
        int toYear, int toMonth)
    {
        var from = new DateTime(fromYear, fromMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(toYear, toMonth, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(1).AddTicks(-1);
        return new InventoryMovementExportPeriod(
            ExportPeriodType.ByMonthRange,
            from,
            to,
            $"Từ tháng {fromMonth:00}/{fromYear} đến tháng {toMonth:00}/{toYear}");
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
        // Readable display name: "Biến động Kho {depot} Tháng {period}"
        var displayPeriod = PeriodType == ExportPeriodType.ByYear
            ? DisplayTitle          // "Năm 2026"
            : DisplayTitle;         // "Tháng 03/2026" or range
        return $"Biến động Kho {safeName} {displayPeriod}.xlsx";
    }
}
