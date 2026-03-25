using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;

public class GetVictimsByPeriodHandler(
    IDashboardRepository dashboardRepository,
    ILogger<GetVictimsByPeriodHandler> logger
) : IRequestHandler<GetVictimsByPeriodQuery, List<VictimsByPeriodDto>>
{
    private static readonly HashSet<string> AllowedGranularities =
        new(StringComparer.OrdinalIgnoreCase) { "day", "week", "month" };

    public async Task<List<VictimsByPeriodDto>> Handle(
        GetVictimsByPeriodQuery request,
        CancellationToken cancellationToken)
    {
        var to = request.To?.ToUniversalTime() ?? DateTime.UtcNow;
        var from = request.From?.ToUniversalTime() ?? to.AddMonths(-6);

        // Normalize: ensure from <= to
        if (from > to) (from, to) = (to, from);

        var granularity = request.Granularity?.ToLowerInvariant() ?? "month";
        if (!AllowedGranularities.Contains(granularity))
            granularity = "month";

        logger.LogInformation(
            "GetVictimsByPeriod: from={from}, to={to}, granularity={granularity}",
            from, to, granularity);

        return await dashboardRepository.GetVictimsByPeriodAsync(
            from, to, granularity, request.Statuses, cancellationToken);
    }
}
