using Microsoft.EntityFrameworkCore;
using RESQ.Application.Extensions;
using RESQ.Application.Services;
using RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerOverview;
using RESQ.Domain.Enum.Identity;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Services.Dashboard;

public class RescuerOverviewDashboardService(
    ResQDbContext context,
    TimeProvider timeProvider) : IRescuerOverviewDashboardService
{
    private const string Timezone = "Asia/Ho_Chi_Minh";

    public async Task<RescuerOverviewResponse> GetOverviewAsync(
        int months,
        CancellationToken cancellationToken = default)
    {
        if (months is < 1 or > 24)
        {
            throw new ArgumentOutOfRangeException(nameof(months), "months must be between 1 and 24.");
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var nowVietnam = nowUtc.ToVietnamTime();
        var currentMonthStart = new DateTime(nowVietnam.Year, nowVietnam.Month, 1);
        var firstMonthStart = currentMonthStart.AddMonths(-(months - 1));

        var rawRows = await context.Users
            .AsNoTracking()
            .Where(user => user.RescuerProfile != null
                && user.RescuerProfile.IsEligibleRescuer
                && user.RescuerProfile.RescuerType != null
                && user.RescuerProfile.RescuerType != string.Empty)
            .Select(user => new RescuerOverviewRawRow(
                user.Id,
                user.RescuerProfile!.RescuerType!,
                user.IsBanned,
                user.RescuerProfile.ApprovedAt ?? user.CreatedAt))
            .ToListAsync(cancellationToken);

        var rescuers = rawRows
            .Select(row => MapEligibleRescuer(row, nowUtc))
            .Where(row => row is not null)
            .Select(row => row!)
            .ToList();

        var monthly = Enumerable.Range(0, months)
            .Select(index => BuildMonthlyBucket(
                firstMonthStart.AddMonths(index),
                rescuers))
            .ToList();

        var currentMonthNewCount = CountJoinedInMonth(rescuers, currentMonthStart);
        var previousNewCount = CountJoinedInMonth(rescuers, currentMonthStart.AddMonths(-1));

        var peakMonth = monthly.Any(item => item.NewCount > 0)
            ? monthly.Last(item => item.NewCount == monthly.Max(month => month.NewCount))
            : BuildEmptyPeakMonth(currentMonthStart);

        var bannedCount = rescuers.Count(row => row.IsBanned);

        return new RescuerOverviewResponse
        {
            GeneratedAt = nowUtc,
            Timezone = Timezone,
            Totals = new RescuerOverviewTotalsDto
            {
                Total = rescuers.Count,
                Core = rescuers.Count(row => row.RescuerType == RescuerType.Core),
                Volunteer = rescuers.Count(row => row.RescuerType == RescuerType.Volunteer),
                Active = rescuers.Count - bannedCount,
                Banned = bannedCount
            },
            ThisMonth = new RescuerOverviewThisMonthDto
            {
                Month = currentMonthStart.Month,
                Year = currentMonthStart.Year,
                NewCount = currentMonthNewCount,
                PreviousNewCount = previousNewCount,
                GrowthPercent = CalculateGrowthPercent(currentMonthNewCount, previousNewCount)
            },
            PeakMonth = new RescuerOverviewPeakMonthDto
            {
                Month = peakMonth.Month,
                Year = peakMonth.Year,
                MonthLabel = peakMonth.MonthLabel,
                NewCount = peakMonth.NewCount
            },
            Monthly = monthly
        };
    }

    private static RescuerOverviewMonthlyDto BuildMonthlyBucket(
        DateTime monthStart,
        IReadOnlyCollection<EligibleRescuerOverviewRow> rescuers)
    {
        var nextMonthStart = monthStart.AddMonths(1);
        var newRescuers = rescuers
            .Where(row => row.JoinedAtVietnam >= monthStart && row.JoinedAtVietnam < nextMonthStart)
            .ToList();

        return new RescuerOverviewMonthlyDto
        {
            Month = monthStart.Month,
            Year = monthStart.Year,
            MonthLabel = BuildMonthLabel(monthStart),
            Total = rescuers.Count(row => row.JoinedAtVietnam < nextMonthStart),
            NewCount = newRescuers.Count,
            Core = newRescuers.Count(row => row.RescuerType == RescuerType.Core),
            Volunteer = newRescuers.Count(row => row.RescuerType == RescuerType.Volunteer)
        };
    }

    private static RescuerOverviewMonthlyDto BuildEmptyPeakMonth(DateTime currentMonthStart) =>
        new()
        {
            Month = currentMonthStart.Month,
            Year = currentMonthStart.Year,
            MonthLabel = BuildMonthLabel(currentMonthStart),
            NewCount = 0
        };

    private static int CountJoinedInMonth(
        IEnumerable<EligibleRescuerOverviewRow> rescuers,
        DateTime monthStart)
    {
        var nextMonthStart = monthStart.AddMonths(1);
        return rescuers.Count(row => row.JoinedAtVietnam >= monthStart && row.JoinedAtVietnam < nextMonthStart);
    }

    private static int CalculateGrowthPercent(int newCount, int previousNewCount)
    {
        if (previousNewCount > 0)
        {
            return (int)Math.Round((double)(newCount - previousNewCount) / previousNewCount * 100);
        }

        return newCount > 0 ? 100 : 0;
    }

    private static EligibleRescuerOverviewRow? MapEligibleRescuer(
        RescuerOverviewRawRow row,
        DateTime nowUtc)
    {
        if (row.JoinedAt is null || !TryMapRescuerType(row.RescuerType, out var rescuerType))
        {
            return null;
        }

        var joinedAtUtc = EnsureUtc(row.JoinedAt.Value);
        if (joinedAtUtc > nowUtc)
        {
            return null;
        }

        return new EligibleRescuerOverviewRow(
            row.Id,
            rescuerType,
            row.IsBanned,
            joinedAtUtc.ToVietnamTime());
    }

    private static bool TryMapRescuerType(string rescuerType, out RescuerType parsedType)
    {
        if (string.Equals(rescuerType, RescuerType.Core.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            parsedType = RescuerType.Core;
            return true;
        }

        if (string.Equals(rescuerType, RescuerType.Volunteer.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            parsedType = RescuerType.Volunteer;
            return true;
        }

        parsedType = default;
        return false;
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static string BuildMonthLabel(DateTime monthStart) => $"Th{monthStart.Month}";

    private sealed record RescuerOverviewRawRow(
        Guid Id,
        string RescuerType,
        bool IsBanned,
        DateTime? JoinedAt);

    private sealed record EligibleRescuerOverviewRow(
        Guid Id,
        RescuerType RescuerType,
        bool IsBanned,
        DateTime JoinedAtVietnam);
}
