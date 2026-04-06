using MediatR;
using RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;

namespace RESQ.Application.UseCases.Operations.Commands.ReportMissionActivityIncident;

public record ReportMissionActivityIncidentCommand(
    int MissionId,
    int ActivityId,
    string Description,
    double? Latitude,
    double? Longitude,
    Guid ReportedBy
) : IRequest<ReportTeamIncidentResponse>;