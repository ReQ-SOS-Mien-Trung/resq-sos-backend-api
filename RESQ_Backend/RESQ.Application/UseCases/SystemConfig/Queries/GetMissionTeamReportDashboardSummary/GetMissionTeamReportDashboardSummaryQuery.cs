using MediatR;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetMissionTeamReportDashboardSummary;

public record GetMissionTeamReportDashboardSummaryQuery(
    IReadOnlyCollection<MissionTeamReportStatus>? ReportStatuses = null)
    : IRequest<MissionTeamReportDashboardSummaryResponse>;
