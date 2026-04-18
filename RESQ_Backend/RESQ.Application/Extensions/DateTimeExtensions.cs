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

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    /// <summary>Chuyển DateTime UTC sang giờ Việt Nam (+7). Core conversion logic.</summary>
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

    /// <summary>Nullable version của ToVietnamTime.</summary>
    public static DateTime? ToVietnamTime(this DateTime? utcDateTime)
        => utcDateTime?.ToVietnamTime();

    /// <summary>
    /// Chuyển DateTime UTC sang DateTimeOffset +07:00 để trả về client.
    /// Dùng ToVietnamTime() làm core, wrap với VietnamOffset để tránh trùng lặp logic.
    /// </summary>
    public static DateTimeOffset ToVietnamOffset(this DateTime utcDateTime)
        => new DateTimeOffset(utcDateTime.ToVietnamTime(), VietnamOffset);

    /// <summary>Nullable version của ToVietnamOffset.</summary>
    public static DateTimeOffset? ToVietnamOffset(this DateTime? utcDateTime)
        => utcDateTime?.ToVietnamOffset();

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
        => dt?.ToUtcForStorage();
}
