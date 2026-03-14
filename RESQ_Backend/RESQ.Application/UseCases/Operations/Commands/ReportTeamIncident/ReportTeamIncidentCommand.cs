using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;

public record ReportTeamIncidentCommand(
    int MissionTeamId,
    string Description,
    double? Latitude,
    double? Longitude,
    Guid ReportedBy
) : IRequest<ReportTeamIncidentResponse>;
