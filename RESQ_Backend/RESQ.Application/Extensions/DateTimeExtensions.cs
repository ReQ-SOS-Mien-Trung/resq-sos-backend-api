using System.Runtime.InteropServices;

namespace RESQ.Application.Extensions;

/// <summary>
/// Date/time helpers for storing UTC in the database and returning Vietnam time to clients.
/// </summary>
public static class DateTimeExtensions
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private static readonly TimeZoneInfo VietnamTimeZone = CreateVietnamTimeZone();

    private static TimeZoneInfo CreateVietnamTimeZone()
    {
        var timeZoneIds = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh", "Asia/Bangkok" }
            : new[] { "Asia/Ho_Chi_Minh", "Asia/Bangkok", "SE Asia Standard Time" };

        foreach (var timeZoneId in timeZoneIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            id: "Vietnam Standard Time",
            baseUtcOffset: VietnamOffset,
            displayName: "(UTC+07:00) Vietnam",
            standardDisplayName: "Vietnam Standard Time");
    }

    /// <summary>
    /// Converts a UTC DateTime to Vietnam time (+07:00).
    /// Unspecified values are treated as UTC to match storage assumptions.
    /// </summary>
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

    /// <summary>Nullable version of ToVietnamTime.</summary>
    public static DateTime? ToVietnamTime(this DateTime? utcDateTime)
        => utcDateTime?.ToVietnamTime();

    /// <summary>
    /// Converts a UTC DateTime to a +07:00 DateTimeOffset for API responses.
    /// </summary>
    public static DateTimeOffset ToVietnamOffset(this DateTime utcDateTime)
        => new DateTimeOffset(utcDateTime.ToVietnamTime(), VietnamOffset);

    /// <summary>Nullable version of ToVietnamOffset.</summary>
    public static DateTimeOffset? ToVietnamOffset(this DateTime? utcDateTime)
        => utcDateTime?.ToVietnamOffset();

    /// <summary>
    /// Ensures the stored value is UTC.
    /// Unspecified values are treated as UTC when the client omits a timezone suffix.
    /// </summary>
    public static DateTime ToUtcForStorage(this DateTime dt)
        => dt.Kind == DateTimeKind.Utc
            ? dt
            : dt.Kind == DateTimeKind.Local
                ? dt.ToUniversalTime()
                : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    /// <summary>Nullable version of ToUtcForStorage.</summary>
    public static DateTime? ToUtcForStorage(this DateTime? dt)
        => dt?.ToUtcForStorage();
}
