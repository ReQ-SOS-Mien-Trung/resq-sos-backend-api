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

        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        var (currentCount, previousCount) = await dashboardRepository.GetRescuerDailyCountsAsync(today, yesterday, cancellationToken);

        var changeValue = currentCount - previousCount;
        string changeDirection;
        double? changePercent;
        string comparisonLabel;

        if (previousCount == 0)
        {
            changeDirection = "new";
            changePercent = null;
            comparisonLabel = "New today";
        }
        else if (changeValue > 0)
        {
            changeDirection = "increase";
            changePercent = Math.Round((double)changeValue / previousCount * 100, 2);
            comparisonLabel = "Compared to yesterday";
        }
        else if (changeValue < 0)
        {
            changeDirection = "decrease";
            changePercent = Math.Round(Math.Abs((double)changeValue) / previousCount * 100, 2);
            comparisonLabel = "Compared to yesterday";
        }
        else
        {
            changeDirection = "no_change";
            changePercent = 0;
            comparisonLabel = "Compared to yesterday";
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
                ComparisonPeriod = "yesterday",
                ComparisonLabel = comparisonLabel
            }
        };
    }
}
