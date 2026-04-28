using Microsoft.EntityFrameworkCore;
using RESQ.Domain.Enum.Identity;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Services.Dashboard;

namespace RESQ.Tests.Infrastructure.System;

public class RescuerOverviewDashboardServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 4, 28, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetOverviewAsync_NoEligibleRescuers_ReturnsZeroSeries()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.GetOverviewAsync(12);

        Assert.Equal(FixedNow.UtcDateTime, result.GeneratedAt);
        Assert.Equal("Asia/Ho_Chi_Minh", result.Timezone);
        Assert.Equal(0, result.Totals.Total);
        Assert.Equal(0, result.Totals.Core);
        Assert.Equal(0, result.Totals.Volunteer);
        Assert.Equal(0, result.Totals.Active);
        Assert.Equal(0, result.Totals.Banned);
        Assert.Equal(12, result.Monthly.Count);
        Assert.All(result.Monthly, month =>
        {
            Assert.Equal(0, month.Total);
            Assert.Equal(0, month.NewCount);
            Assert.Equal(0, month.Core);
            Assert.Equal(0, month.Volunteer);
        });
        Assert.Equal(4, result.PeakMonth.Month);
        Assert.Equal(2026, result.PeakMonth.Year);
        Assert.Equal("Th4", result.PeakMonth.MonthLabel);
        Assert.Equal(0, result.PeakMonth.NewCount);
        Assert.Equal(0, result.ThisMonth.NewCount);
        Assert.Equal(0, result.ThisMonth.PreviousNewCount);
        Assert.Equal(0, result.ThisMonth.GrowthPercent);
    }

    [Fact]
    public async Task GetOverviewAsync_CurrentMonthCoreRescuer_CountsCurrentAndPeak()
    {
        await using var context = CreateContext();
        AddUser(context, RescuerType.Core.ToString(), true, false, Utc(2026, 4, 5));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetOverviewAsync(12);

        Assert.Equal(1, result.Totals.Total);
        Assert.Equal(1, result.Totals.Core);
        Assert.Equal(0, result.Totals.Volunteer);
        Assert.Equal(1, result.Totals.Active);
        Assert.Equal(0, result.Totals.Banned);

        var currentMonth = result.Monthly.Last();
        Assert.Equal(4, currentMonth.Month);
        Assert.Equal(2026, currentMonth.Year);
        Assert.Equal(1, currentMonth.Total);
        Assert.Equal(1, currentMonth.NewCount);
        Assert.Equal(1, currentMonth.Core);
        Assert.Equal(0, currentMonth.Volunteer);
        Assert.Equal(4, result.PeakMonth.Month);
        Assert.Equal(1, result.PeakMonth.NewCount);
    }

    [Fact]
    public async Task GetOverviewAsync_GrowthPercent_ComparesThisMonthToPreviousMonth()
    {
        await using var context = CreateContext();
        for (var index = 0; index < 5; index++)
        {
            AddUser(context, RescuerType.Volunteer.ToString(), true, false, Utc(2026, 3, index + 1));
        }

        for (var index = 0; index < 15; index++)
        {
            AddUser(context, RescuerType.Volunteer.ToString(), true, false, Utc(2026, 4, index + 1));
        }

        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetOverviewAsync(12);

        Assert.Equal(15, result.ThisMonth.NewCount);
        Assert.Equal(5, result.ThisMonth.PreviousNewCount);
        Assert.Equal(200, result.ThisMonth.GrowthPercent);
    }

    [Fact]
    public async Task GetOverviewAsync_BannedRescuers_AreIncludedInTotalAndBanned()
    {
        await using var context = CreateContext();
        AddUser(context, RescuerType.Core.ToString(), true, false, Utc(2026, 4, 1));
        AddUser(context, RescuerType.Volunteer.ToString(), true, true, Utc(2026, 4, 2));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetOverviewAsync(12);

        Assert.Equal(2, result.Totals.Total);
        Assert.Equal(1, result.Totals.Banned);
        Assert.Equal(1, result.Totals.Active);
        Assert.Equal(1, result.Totals.Core);
        Assert.Equal(1, result.Totals.Volunteer);
    }

    [Fact]
    public async Task GetOverviewAsync_IneligibleOrInvalidRescuers_AreExcluded()
    {
        await using var context = CreateContext();
        AddUser(context, RescuerType.Core.ToString(), false, false, Utc(2026, 4, 1));
        AddUser(context, RescuerType.Volunteer.ToString(), false, true, Utc(2026, 4, 2));
        AddUser(context, null, true, false, Utc(2026, 4, 3));
        AddUser(context, string.Empty, true, false, Utc(2026, 4, 4));
        AddUser(context, "Coordinator", true, false, Utc(2026, 4, 5));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetOverviewAsync(12);

        Assert.Equal(0, result.Totals.Total);
        Assert.Equal(0, result.Monthly.Last().NewCount);
    }

    [Fact]
    public async Task GetOverviewAsync_MonthsParameter_ControlsSeriesLength()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.GetOverviewAsync(6);

        Assert.Equal(6, result.Monthly.Count);
        Assert.Equal(11, result.Monthly.First().Month);
        Assert.Equal(2025, result.Monthly.First().Year);
        Assert.Equal(4, result.Monthly.Last().Month);
        Assert.Equal(2026, result.Monthly.Last().Year);
    }

    [Fact]
    public async Task GetOverviewAsync_UsesVietnamMonthAndSkipsFutureJoinedAt()
    {
        await using var context = CreateContext();
        AddUser(context, RescuerType.Core.ToString(), true, false, Utc(2026, 3, 31, 18));
        AddUser(context, RescuerType.Volunteer.ToString(), true, false, Utc(2026, 4, 30));
        AddUser(context, RescuerType.Volunteer.ToString(), true, false, approvedAt: null, createdAt: Utc(2026, 4, 10));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetOverviewAsync(12);

        var march = result.Monthly.Single(month => month.Year == 2026 && month.Month == 3);
        var april = result.Monthly.Single(month => month.Year == 2026 && month.Month == 4);
        Assert.Equal(0, march.NewCount);
        Assert.Equal(2, april.NewCount);
        Assert.Equal(1, april.Core);
        Assert.Equal(1, april.Volunteer);
        Assert.Equal(2, result.Totals.Total);
    }

    private static RescuerOverviewDashboardService CreateService(ResQDbContext context) =>
        new(context, new FixedTimeProvider(FixedNow));

    private static ResQDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ResQDbContext(options);
    }

    private static void AddUser(
        ResQDbContext context,
        string? rescuerType,
        bool isEligible,
        bool isBanned,
        DateTime? approvedAt,
        DateTime? createdAt = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            RoleId = 3,
            Username = Guid.NewGuid().ToString("N"),
            Password = "password",
            CreatedAt = createdAt ?? approvedAt ?? Utc(2026, 1, 1),
            IsBanned = isBanned
        };

        user.RescuerProfile = new RescuerProfile
        {
            UserId = user.Id,
            User = user,
            RescuerType = rescuerType,
            IsEligibleRescuer = isEligible,
            ApprovedAt = approvedAt
        };

        context.Users.Add(user);
    }

    private static DateTime Utc(int year, int month, int day, int hour = 0) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
