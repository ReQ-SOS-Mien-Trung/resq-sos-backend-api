using MediatR;
using RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;
using RESQ.Application.UseCases.Operations.Shared;

namespace RESQ.Application.UseCases.Operations.Commands.ReportMissionTeamIncident;

public record ReportMissionTeamIncidentCommand(
    int MissionId,
    int MissionTeamId,
    MissionIncidentReportRequest Payload,
    Guid ReportedBy
) : IRequest<ReportTeamIncidentResponse>;
