using RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerOverview;

namespace RESQ.Application.Services;

public interface IRescuerOverviewDashboardService
{
    Task<RescuerOverviewResponse> GetOverviewAsync(
        int months,
        CancellationToken cancellationToken = default);
}
