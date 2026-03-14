using MediatR;
using RESQ.Application.Repositories.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetTeamIncidents;

public class GetTeamIncidentsQueryHandler(
    ITeamIncidentRepository teamIncidentRepository
) : IRequestHandler<GetTeamIncidentsQuery, GetTeamIncidentsResponse>
{
    public async Task<GetTeamIncidentsResponse> Handle(GetTeamIncidentsQuery request, CancellationToken cancellationToken)
    {
        var incidents = await teamIncidentRepository.GetByMissionIdAsync(request.MissionId, cancellationToken);

        return new GetTeamIncidentsResponse
        {
            MissionId = request.MissionId,
            Incidents = incidents.Select(i => new TeamIncidentDto
            {
                IncidentId = i.Id,
                MissionTeamId = i.MissionTeamId,
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
