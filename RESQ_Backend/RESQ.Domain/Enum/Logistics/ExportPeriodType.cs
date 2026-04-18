namespace RESQ.Domain.Enum.Logistics;

public enum ExportPeriodType
{
    /// <summary>Khoảng ngày tùy chọn (FromDate → ToDate).</summary>
    ByDateRange,

    /// <summary>Theo tháng + năm cụ thể.</summary>
    ByMonth
}
