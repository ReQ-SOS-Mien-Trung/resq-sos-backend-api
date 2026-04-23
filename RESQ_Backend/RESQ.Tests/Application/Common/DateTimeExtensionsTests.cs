using RESQ.Application.Extensions;

namespace RESQ.Tests.Application.Common;

public class DateTimeExtensionsTests
{
    [Fact]
    public void ToVietnamTime_ConvertsUtcToVietnamTime()
    {
        var utcDateTime = new DateTime(2026, 4, 23, 12, 30, 0, DateTimeKind.Utc);

        var result = utcDateTime.ToVietnamTime();

        Assert.Equal(new DateTime(2026, 4, 23, 19, 30, 0), result);
    }

    [Fact]
    public void ToVietnamTime_TreatsUnspecifiedValuesAsUtc()
    {
        var unspecifiedDateTime = new DateTime(2026, 4, 23, 12, 30, 0, DateTimeKind.Unspecified);

        var result = unspecifiedDateTime.ToVietnamTime();

        Assert.Equal(new DateTime(2026, 4, 23, 19, 30, 0), result);
    }

    [Fact]
    public void ToVietnamOffset_ReturnsUtcPlusSevenOffset()
    {
        var utcDateTime = new DateTime(2026, 4, 23, 12, 30, 0, DateTimeKind.Utc);

        var result = utcDateTime.ToVietnamOffset();

        Assert.Equal(TimeSpan.FromHours(7), result.Offset);
        Assert.Equal(new DateTime(2026, 4, 23, 19, 30, 0), result.DateTime);
    }
}
