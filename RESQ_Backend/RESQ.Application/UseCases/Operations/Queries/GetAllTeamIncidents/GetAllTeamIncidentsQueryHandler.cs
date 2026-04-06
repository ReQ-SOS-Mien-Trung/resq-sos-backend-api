using MediatR;
using RESQ.Application.Repositories.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetAllTeamIncidents;

public class GetAllTeamIncidentsQueryHandler(
    ITeamIncidentRepository teamIncidentRepository
) : IRequestHandler<GetAllTeamIncidentsQuery, GetAllTeamIncidentsResponse>
{
    public async Task<GetAllTeamIncidentsResponse> Handle(GetAllTeamIncidentsQuery request, CancellationToken cancellationToken)
    {
        var incidents = await teamIncidentRepository.GetAllAsync(cancellationToken);

        return new GetAllTeamIncidentsResponse
        {
            Incidents = incidents.Select(i => new TeamIncidentDto
            {
                IncidentId = i.Id,
                MissionTeamId = i.MissionTeamId,
                MissionActivityId = i.MissionActivityId,
                IncidentScope = i.IncidentScope.ToString(),
                Latitude = i.Latitude,
                Longitude = i.Longitude,
                Description = i.Description,
                Status = i.Status.ToString(),
                ReportedBy = i.ReportedBy,
                ReportedAt = i.ReportedAt
            }).ToList()
        };
    }
}
