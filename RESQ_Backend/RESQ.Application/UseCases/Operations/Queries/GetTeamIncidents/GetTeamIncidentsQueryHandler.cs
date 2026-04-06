using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetTeamIncidents;

public class GetTeamIncidentsQueryHandler(
    ITeamIncidentRepository teamIncidentRepository,
    IUserRepository userRepository
) : IRequestHandler<GetTeamIncidentsQuery, GetTeamIncidentsResponse>
{
    public async Task<GetTeamIncidentsResponse> Handle(GetTeamIncidentsQuery request, CancellationToken cancellationToken)
    {
        var incidents = await teamIncidentRepository.GetByMissionIdAsync(request.MissionId, cancellationToken);
        var incidentList = incidents.ToList();

        var reporterIds = incidentList
            .Where(i => i.ReportedBy.HasValue)
            .Select(i => i.ReportedBy!.Value)
            .Distinct()
            .ToList();

        var userTasks = reporterIds.Select(id => userRepository.GetByIdAsync(id, cancellationToken));
        var users = await Task.WhenAll(userTasks);
        var userLookup = users
            .Where(u => u is not null)
            .ToDictionary(u => u!.Id, u => u!);

        return new GetTeamIncidentsResponse
        {
            MissionId = request.MissionId,
            Incidents = incidentList.Select(i =>
            {
                ReportedByDto? reportedBy = null;
                if (i.ReportedBy.HasValue && userLookup.TryGetValue(i.ReportedBy.Value, out var user))
                {
                    reportedBy = new ReportedByDto
                    {
                        Id        = user.Id,
                        FirstName = user.FirstName,
                        LastName  = user.LastName,
                        Phone     = user.Phone,
                        Email     = user.Email,
                        AvatarUrl = user.AvatarUrl
                    };
                }

                return new TeamIncidentDto
                {
                    IncidentId       = i.Id,
                    MissionTeamId    = i.MissionTeamId,
                    MissionActivityId = i.MissionActivityId,
                    IncidentScope    = i.IncidentScope.ToString(),
                    Latitude         = i.Latitude,
                    Longitude        = i.Longitude,
                    Description      = i.Description,
                    Status           = i.Status.ToString(),
                    ReportedBy       = reportedBy,
                    ReportedAt       = i.ReportedAt
                };
            }).ToList()
        };
    }
}
