using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescuersDailyStatistics;

public class GetRescuersDailyStatisticsHandler(
    IDashboardRepository dashboardRepository,
    ILogger<GetRescuersDailyStatisticsHandler> logger
) : IRequestHandler<GetRescuersDailyStatisticsQuery, GetRescuersDailyStatisticsResponse>
{
    public async Task<GetRescuersDailyStatisticsResponse> Handle(
        GetRescuersDailyStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("GetRescuersDailyStatistics: fetching today and yesterday rescuer counts");

        // Boundary theo giờ Việt Nam (UTC+7): nửa đêm VN = 17:00 UTC hôm trước
        var vnOffset = TimeSpan.FromHours(7);
        var todayStartUtc = DateTime.SpecifyKind((DateTime.UtcNow + vnOffset).Date - vnOffset, DateTimeKind.Utc);
        var yesterday = todayStartUtc.AddDays(-1);
        var today = todayStartUtc;

        var (currentCount, previousCount) = await dashboardRepository.GetRescuerDailyCountsAsync(today, yesterday, cancellationToken);

        var changeValue = currentCount - previousCount;
        string changeDirection;
        double? changePercent;
        string comparisonLabel;

        if (previousCount == 0)
        {
            changeDirection = "new";
            changePercent = null;
            comparisonLabel = "Mới hôm nay";
        }
        else if (changeValue > 0)
        {
            changeDirection = "increase";
            changePercent = Math.Round((double)changeValue / previousCount * 100, 2);
            comparisonLabel = "So với hôm qua";
        }
        else if (changeValue < 0)
        {
            changeDirection = "decrease";
            changePercent = Math.Round(Math.Abs((double)changeValue) / previousCount * 100, 2);
            comparisonLabel = "So với hôm qua";
        }
        else
        {
            changeDirection = "no_change";
            changePercent = 0;
            comparisonLabel = "So với hôm qua";
        }

        return new GetRescuersDailyStatisticsResponse
        {
            TotalRescuers = currentCount,
            DailyChange = new GetRescuersDailyStatisticsResponse.DailyChangeDto
            {
                CurrentCount = currentCount,
                PreviousCount = previousCount,
                ChangeValue = changeValue,
                ChangePercent = changePercent,
                ChangeDirection = changeDirection,
                ComparisonPeriod = "hôm qua",
                ComparisonLabel = comparisonLabel
            }
        };
    }
}
