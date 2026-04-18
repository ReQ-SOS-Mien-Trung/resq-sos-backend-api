using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Queries.Shared;

namespace RESQ.Application.UseCases.Operations.Queries.GetAllTeamIncidents;

public class GetAllTeamIncidentsQueryHandler(
    ITeamIncidentRepository teamIncidentRepository,
    IUserRepository userRepository
) : IRequestHandler<GetAllTeamIncidentsQuery, GetAllTeamIncidentsResponse>
{
    public async Task<GetAllTeamIncidentsResponse> Handle(GetAllTeamIncidentsQuery request, CancellationToken cancellationToken)
    {
        var incidents = await teamIncidentRepository.GetAllAsync(cancellationToken);
        var incidentList = incidents.ToList();
        var reporterIds = incidentList
            .Where(i => i.ReportedBy.HasValue)
            .Select(i => i.ReportedBy!.Value)
            .Distinct()
            .ToList();

        var users = await Task.WhenAll(reporterIds.Select(id => userRepository.GetByIdAsync(id, cancellationToken)));
        var userLookup = users
            .Where(user => user is not null)
            .ToDictionary(user => user!.Id, user => user!);

        return new GetAllTeamIncidentsResponse
        {
            Incidents = incidentList.Select(i =>
            {
                ReportedByDto? reportedBy = null;
                if (i.ReportedBy.HasValue && userLookup.TryGetValue(i.ReportedBy.Value, out var user))
                {
                    reportedBy = new ReportedByDto
                    {
                        Id = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Phone = user.Phone,
                        Email = user.Email,
                        AvatarUrl = user.AvatarUrl
                    };
                }

                return TeamIncidentQueryDtoMapper.ToDto(i, reportedBy);
            }).ToList()
        };
    }
}
