using RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;

namespace RESQ.Application.Repositories.System;

public interface IDashboardRepository
{
    /// <summary>
    /// Trả về tổng số victim (adult + child + elderly từ structured_data)
    /// nhóm theo khoảng thời gian (day / week / month).
    /// </summary>
    Task<List<VictimsByPeriodDto>> GetVictimsByPeriodAsync(
        DateTime from,
        DateTime to,
        string granularity,
        CancellationToken cancellationToken = default);
}
