using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetMissionTeamReportsDashboard;

public record GetMissionTeamReportsDashboardQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? ReportStatus = null,
    int? TeamId = null,
    string? Search = null) : IRequest<PagedResult<MissionTeamReportDashboardItemDto>>;
