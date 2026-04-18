using RESQ.Domain.Enum.Logistics;

namespace RESQ.Domain.Entities.Logistics.ValueObjects;

/// <summary>
/// Kết quả đánh giá tồn kho theo warning band mới.
/// </summary>
public sealed record StockWarningResult(
    decimal SeverityRatio,
    string Level,
    ThresholdResolutionScope ResolvedScope,
    /// <summary>Giá trị minimumThreshold đã được resolve. Null khi UNCONFIGURED.</summary>
    int? ResolvedThreshold
)
{
    /// <summary>
    /// True khi threshold được lấy từ Global config (không có config riêng cho item/category/depot).
    /// FE dùng để hiển thị cảnh báo "đang dùng ngưỡng mặc định toàn hệ thống".
    /// </summary>
    public bool IsUsingGlobalDefault => ResolvedScope == ThresholdResolutionScope.Global;

    public static readonly StockWarningResult Unconfigured =
        new(0m, StockWarningLevel.Unconfigured, ThresholdResolutionScope.None, null);
}

/// <summary>Các hằng tên level - tránh string literal rải rác trong code.</summary>
public static class StockWarningLevel
{
    public const string Ok           = "OK";
    public const string Low          = "LOW";
    public const string Medium       = "MEDIUM";
    public const string Critical     = "CRITICAL";
    public const string Unconfigured = "UNCONFIGURED";
}
