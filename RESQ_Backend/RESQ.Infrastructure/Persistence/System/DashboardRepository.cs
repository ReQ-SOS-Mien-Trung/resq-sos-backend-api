using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.System;

public class DashboardRepository(ResQDbContext context) : IDashboardRepository
{
    private readonly ResQDbContext _context = context;

    /// <inheritdoc/>
    public async Task<List<VictimsByPeriodDto>> GetVictimsByPeriodAsync(
        DateTime from,
        DateTime to,
        string granularity,
        List<string>? statuses,
        CancellationToken cancellationToken = default)
    {
        // Build optional status filter clause
        var statusFilter = statuses != null && statuses.Count > 0
            ? "AND status = ANY(@statuses)"
            : string.Empty;

        var sql = $"""
            SELECT
                date_trunc(@granularity, received_at) AS "Period",
                COALESCE(SUM(
                    COALESCE((structured_data::jsonb -> 'people_count' ->> 'adult')::int, 0)
                  + COALESCE((structured_data::jsonb -> 'people_count' ->> 'child')::int, 0)
                  + COALESCE((structured_data::jsonb -> 'people_count' ->> 'elderly')::int, 0)
                ), 0)::int AS "TotalVictims"
            FROM sos_requests
            WHERE received_at >= @from
              AND received_at <= @to
              AND structured_data IS NOT NULL
              AND structured_data <> ''
              {statusFilter}
            GROUP BY date_trunc(@granularity, received_at)
            ORDER BY date_trunc(@granularity, received_at)
            """;

        var fromParam = new Npgsql.NpgsqlParameter("from", from);
        var toParam = new Npgsql.NpgsqlParameter("to", to);
        var granularityParam = new Npgsql.NpgsqlParameter("granularity", granularity);

        List<object> parameters = [fromParam, toParam, granularityParam];

        if (statuses != null && statuses.Count > 0)
        {
            var statusesParam = new Npgsql.NpgsqlParameter("statuses", statuses.ToArray())
            {
                DataTypeName = "text[]"
            };
            parameters.Add(statusesParam);
        }

        var results = await _context.Database
            .SqlQueryRaw<VictimsByPeriodRaw>(sql, parameters.ToArray())
            .ToListAsync(cancellationToken);

        return results.Select(r => new VictimsByPeriodDto
        {
            Period = r.Period,
            TotalVictims = r.TotalVictims
        }).ToList();
    }

    // Internal projection class for raw SQL result
    private sealed class VictimsByPeriodRaw
    {
        public DateTime Period { get; set; }
        public int TotalVictims { get; set; }
    }
}
