using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetMissionTeamReportsDashboard;

public class GetMissionTeamReportsDashboardHandler(
    IDashboardRepository dashboardRepository,
    ILogger<GetMissionTeamReportsDashboardHandler> logger)
    : IRequestHandler<GetMissionTeamReportsDashboardQuery, PagedResult<MissionTeamReportDashboardItemDto>>
{
    public async Task<PagedResult<MissionTeamReportDashboardItemDto>> Handle(
        GetMissionTeamReportsDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var normalizedReportStatus = NormalizeReportStatus(request.ReportStatus);

        logger.LogInformation(
            "GetMissionTeamReportsDashboard page={page} size={size} reportStatus={reportStatus} teamId={teamId} search={search}",
            request.PageNumber,
            request.PageSize,
            normalizedReportStatus,
            request.TeamId,
            request.Search);

        return await dashboardRepository.GetMissionTeamReportsDashboardAsync(
            request.PageNumber,
            request.PageSize,
            normalizedReportStatus,
            request.TeamId,
            request.Search,
            cancellationToken);
    }

    private static string? NormalizeReportStatus(string? reportStatus)
    {
        if (string.IsNullOrWhiteSpace(reportStatus))
            return null;

        if (Enum.TryParse<MissionTeamReportStatus>(reportStatus.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed.ToString();
        }

        throw new BadRequestException(
            $"Trạng thái report không hợp lệ: '{reportStatus}'. Các giá trị hợp lệ: {string.Join(", ", Enum.GetNames<MissionTeamReportStatus>())}.");
    }
}
