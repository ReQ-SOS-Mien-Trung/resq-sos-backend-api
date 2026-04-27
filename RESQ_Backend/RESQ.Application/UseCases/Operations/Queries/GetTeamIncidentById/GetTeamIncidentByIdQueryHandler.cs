using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Queries.Shared;

namespace RESQ.Application.UseCases.Operations.Queries.GetTeamIncidentById;

public class GetTeamIncidentByIdQueryHandler(
    ITeamIncidentRepository teamIncidentRepository,
    IUserRepository userRepository
) : IRequestHandler<GetTeamIncidentByIdQuery, GetTeamIncidentByIdResponse>
{
    public async Task<GetTeamIncidentByIdResponse> Handle(GetTeamIncidentByIdQuery request, CancellationToken cancellationToken)
    {
        var incident = await teamIncidentRepository.GetByIdAsync(request.IncidentId, cancellationToken);
        if (incident is null)
        {
            throw new NotFoundException($"Không tìm thấy sự cố với ID {request.IncidentId}");
        }

        ReportedByDto? reportedBy = null;
        if (incident.ReportedBy.HasValue)
        {
            var user = await userRepository.GetByIdAsync(incident.ReportedBy.Value, cancellationToken);
            if (user is not null)
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
        }

        return new GetTeamIncidentByIdResponse
        {
            Incident = TeamIncidentQueryDtoMapper.ToDetailDto(incident, reportedBy)
        };
    }
}
