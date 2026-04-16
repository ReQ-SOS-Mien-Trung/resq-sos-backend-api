using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetMissionSuccessRateSummary;

public class GetMissionSuccessRateSummaryHandler(
    IDashboardRepository dashboardRepository,
    ILogger<GetMissionSuccessRateSummaryHandler> logger
) : IRequestHandler<GetMissionSuccessRateSummaryQuery, GetMissionSuccessRateSummaryResponse>
{
    public async Task<GetMissionSuccessRateSummaryResponse> Handle(
        GetMissionSuccessRateSummaryQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("GetMissionSuccessRateSummary: fetching today and yesterday mission stats");

        // Boundary theo giờ Việt Nam (UTC+7): nửa đêm VN = 17:00 UTC hôm trước
        var vnOffset = TimeSpan.FromHours(7);
        var todayStartUtc = DateTime.SpecifyKind((DateTime.UtcNow + vnOffset).Date - vnOffset, DateTimeKind.Utc);
        var yesterday = todayStartUtc.AddDays(-1);
        var today = todayStartUtc;

        var (todayCompleted, todayTotal, yesterdayCompleted, yesterdayTotal) =
            await dashboardRepository.GetMissionFinishedCountsAsync(today, yesterday, cancellationToken);

        var todayRate = todayTotal > 0 ? Math.Round((double)todayCompleted / todayTotal * 100, 2) : 0;
        var yesterdayRate = yesterdayTotal > 0 ? Math.Round((double)yesterdayCompleted / yesterdayTotal * 100, 2) : 0;

        var changePercent = Math.Round(todayRate - yesterdayRate, 2);

        string changeDirection = changePercent switch
        {
            > 0 => "increase",
            < 0 => "decrease",
            _ => "no_change"
        };

        return new GetMissionSuccessRateSummaryResponse
        {
            SuccessRate = todayRate,
            ChangePercent = Math.Abs(changePercent),
            ChangeDirection = changeDirection,
            ComparisonLabel = "So với hôm qua"
        };
    }
}
