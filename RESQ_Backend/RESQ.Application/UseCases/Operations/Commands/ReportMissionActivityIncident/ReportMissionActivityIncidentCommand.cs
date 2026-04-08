using MediatR;
using RESQ.Application.UseCases.Operations.Shared;
namespace RESQ.Application.UseCases.Operations.Commands.ReportMissionActivityIncident;

public record ReportMissionActivityIncidentCommand(
    int MissionId,
    int MissionTeamId,
    ActivityIncidentReportRequest Payload,
    Guid ReportedBy
) : IRequest<RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident.ReportTeamIncidentResponse>;