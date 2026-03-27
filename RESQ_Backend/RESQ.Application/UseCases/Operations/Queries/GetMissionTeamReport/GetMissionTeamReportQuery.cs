using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;

public record GetMissionTeamReportQuery(
    int MissionId,
    int MissionTeamId,
    Guid RequestedBy
) : IRequest<MissionTeamReportResponse>;