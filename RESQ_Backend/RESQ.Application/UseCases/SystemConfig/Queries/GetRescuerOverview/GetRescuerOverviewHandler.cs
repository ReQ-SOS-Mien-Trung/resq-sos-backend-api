using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerOverview;

public class GetRescuerOverviewHandler(
    IRescuerOverviewDashboardService rescuerOverviewDashboardService,
    ILogger<GetRescuerOverviewHandler> logger)
    : IRequestHandler<GetRescuerOverviewQuery, RescuerOverviewResponse>
{
    public async Task<RescuerOverviewResponse> Handle(
        GetRescuerOverviewQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "GetRescuerOverview: fetching approved rescuer overview months={Months}",
            request.Months);

        return await rescuerOverviewDashboardService.GetOverviewAsync(
            request.Months,
            cancellationToken);
    }
}
