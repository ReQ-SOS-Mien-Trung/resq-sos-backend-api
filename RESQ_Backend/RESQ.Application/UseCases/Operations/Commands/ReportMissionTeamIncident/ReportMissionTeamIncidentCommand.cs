using MediatR;
using RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;

namespace RESQ.Application.UseCases.Operations.Commands.ReportMissionTeamIncident;

public record ReportMissionTeamIncidentCommand(
    int MissionId,
    int MissionTeamId,
    string Description,
    double? Latitude,
    double? Longitude,
    Guid ReportedBy
) : IRequest<ReportTeamIncidentResponse>;