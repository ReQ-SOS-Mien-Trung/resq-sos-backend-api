using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamDetail;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamList;
using RESQ.Application.UseCases.SystemConfig.Queries.GetMissionTeamReportDashboardSummary;
using RESQ.Application.UseCases.SystemConfig.Queries.GetMissionTeamReportsDashboard;
using RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerMissionScores;
using RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.Repositories.System;

public interface IDashboardRepository
{
    Task<List<VictimsByPeriodDto>> GetVictimsByPeriodAsync(
        DateTime from,
        DateTime to,
        string granularity,
        CancellationToken cancellationToken = default);

    Task<(int currentCount, int previousCount)> GetRescuerDailyCountsAsync(
        DateTime today,
        DateTime yesterday,
        CancellationToken cancellationToken = default);

    Task<(int todayCompleted, int todayTotal, int yesterdayCompleted, int yesterdayTotal)> GetMissionFinishedCountsAsync(
        DateTime today,
        DateTime yesterday,
        CancellationToken cancellationToken = default);

    Task<(int todayCount, int yesterdayCount)> GetSosRequestDailyCountsAsync(
        DateTime today,
        DateTime yesterday,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AdminTeamListItemDto>> GetAdminTeamListAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminTeamDetailDto?> GetAdminTeamDetailAsync(
        int teamId,
        CancellationToken cancellationToken = default);

    Task<RescuerMissionScoresDto?> GetRescuerMissionScoresAsync(
        Guid rescuerId,
        CancellationToken cancellationToken = default);

    Task<MissionTeamReportDashboardSummaryResponse> GetMissionTeamReportDashboardSummaryAsync(
        IReadOnlyCollection<MissionTeamReportStatus>? reportStatuses = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<MissionTeamReportDashboardItemDto>> GetMissionTeamReportsDashboardAsync(
        int pageNumber,
        int pageSize,
        string? reportStatus = null,
        int? teamId = null,
        string? search = null,
        CancellationToken cancellationToken = default);
}
