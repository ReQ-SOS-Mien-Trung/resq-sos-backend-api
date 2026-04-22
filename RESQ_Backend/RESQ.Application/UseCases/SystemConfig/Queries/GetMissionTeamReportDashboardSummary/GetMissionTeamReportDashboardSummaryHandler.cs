using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetMissionTeamReportDashboardSummary;

public class GetMissionTeamReportDashboardSummaryHandler(
    IDashboardRepository dashboardRepository,
    ILogger<GetMissionTeamReportDashboardSummaryHandler> logger)
    : IRequestHandler<GetMissionTeamReportDashboardSummaryQuery, MissionTeamReportDashboardSummaryResponse>
{
    public async Task<MissionTeamReportDashboardSummaryResponse> Handle(
        GetMissionTeamReportDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("GetMissionTeamReportDashboardSummary: fetching mission team report summary");

        return await dashboardRepository.GetMissionTeamReportDashboardSummaryAsync(cancellationToken);
    }
}
