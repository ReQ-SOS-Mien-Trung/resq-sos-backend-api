namespace RESQ.Application.Extensions;

/// <summary>
/// Tiện ích xử lý múi giờ. Quy tắc:
/// - Lưu DB: luôn UTC (DateTimeKind.Utc)
/// - Trả về client: chuyển sang giờ Việt Nam (UTC+7)
/// </summary>
public static class DateTimeExtensions
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    /// <summary>Chuyển DateTime UTC sang giờ Việt Nam (+7) để trả về client.</summary>
    public static DateTime ToVietnamTime(this DateTime utcDateTime)
    {
        if (utcDateTime == DateTime.MinValue || utcDateTime == DateTime.MaxValue)
            return utcDateTime;

        var ensuredUtc = utcDateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)
            : utcDateTime;

        return TimeZoneInfo.ConvertTimeFromUtc(
            ensuredUtc.Kind == DateTimeKind.Utc ? ensuredUtc : ensuredUtc.ToUniversalTime(),
            VietnamTimeZone);
    }

    /// <summary>Chuyển DateTime? UTC sang giờ Việt Nam (+7) để trả về client.</summary>
    public static DateTime? ToVietnamTime(this DateTime? utcDateTime)
        => utcDateTime.HasValue ? utcDateTime.Value.ToVietnamTime() : null;

    /// <summary>
    /// Đảm bảo DateTime được lưu xuống DB là UTC.
    /// Nếu Kind == Unspecified (client gửi không có 'Z'), giả định là UTC.
    /// </summary>
    public static DateTime ToUtcForStorage(this DateTime dt)
        => dt.Kind == DateTimeKind.Utc
            ? dt
            : dt.Kind == DateTimeKind.Local
                ? dt.ToUniversalTime()
                : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    /// <summary>Nullable version của ToUtcForStorage.</summary>
    public static DateTime? ToUtcForStorage(this DateTime? dt)
        => dt.HasValue ? dt.Value.ToUtcForStorage() : null;
}
