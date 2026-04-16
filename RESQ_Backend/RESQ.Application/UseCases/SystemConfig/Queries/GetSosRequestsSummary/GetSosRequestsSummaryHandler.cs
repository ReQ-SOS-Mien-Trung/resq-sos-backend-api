using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetSosRequestsSummary;

public class GetSosRequestsSummaryHandler(
    IDashboardRepository dashboardRepository,
    ILogger<GetSosRequestsSummaryHandler> logger
) : IRequestHandler<GetSosRequestsSummaryQuery, GetSosRequestsSummaryResponse>
{
    public async Task<GetSosRequestsSummaryResponse> Handle(
        GetSosRequestsSummaryQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("GetSosRequestsSummary: fetching today and yesterday sos counts");

        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        var (todayCount, yesterdayCount) = await dashboardRepository.GetSosRequestDailyCountsAsync(today, yesterday, cancellationToken);

        string changeDirection;
        double? changePercent;
        string comparisonLabel;

        if (yesterdayCount == 0)
        {
            changeDirection = "new";
            changePercent = null;
            comparisonLabel = "mới hôm nay";
        }
        else
        {
            var rawChange = (double)(todayCount - yesterdayCount) / yesterdayCount * 100;
            changePercent = Math.Round(Math.Abs(rawChange), 2);
            changeDirection = rawChange switch
            {
                > 0 => "increase",
                < 0 => "decrease",
                _ => "no_change"
            };
            comparisonLabel = "so với hôm qua";
        }

        return new GetSosRequestsSummaryResponse
        {
            TotalSosRequests = todayCount,
            ChangePercent = changePercent,
            ChangeDirection = changeDirection,
            ComparisonLabel = comparisonLabel
        };
    }
}
