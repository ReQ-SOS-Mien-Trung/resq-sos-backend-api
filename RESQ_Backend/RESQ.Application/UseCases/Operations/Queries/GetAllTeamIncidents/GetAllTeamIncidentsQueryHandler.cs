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
        var pagedIncidents = await teamIncidentRepository.GetPagedAsync(request.PageNumber, request.PageSize, cancellationToken);
        var incidentList = pagedIncidents.Items;
        var reporterIds = incidentList
            .Where(i => i.ReportedBy.HasValue)
            .Select(i => i.ReportedBy!.Value)
            .Distinct()
            .ToList();

        var users = reporterIds.Count == 0
            ? new List<RESQ.Domain.Entities.Identity.UserModel>()
            : await userRepository.GetByIdsAsync(reporterIds, cancellationToken);

        if (users.Count < reporterIds.Count)
        {
            var loadedUserIds = users
                .Select(user => user.Id)
                .ToHashSet();

            foreach (var reporterId in reporterIds)
            {
                if (loadedUserIds.Contains(reporterId))
                {
                    continue;
                }

                var user = await userRepository.GetByIdAsync(reporterId, cancellationToken);
                if (user is null)
                {
                    continue;
                }

                users.Add(user);
                loadedUserIds.Add(user.Id);
            }
        }

        var userLookup = users
            .ToDictionary(user => user.Id, user => user);

        return new GetAllTeamIncidentsResponse
        {
            Items = incidentList.Select(i =>
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
            }).ToList(),
            PageNumber = pagedIncidents.PageNumber,
            PageSize = pagedIncidents.PageSize,
            TotalCount = pagedIncidents.TotalCount,
            TotalPages = pagedIncidents.TotalPages,
            HasNextPage = pagedIncidents.HasNextPage,
            HasPreviousPage = pagedIncidents.HasPreviousPage
        };
    }
}
