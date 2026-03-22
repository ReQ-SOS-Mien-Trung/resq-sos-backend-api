using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAllRescueTeams;

public class GetAllRescueTeamsQueryHandler(IPersonnelQueryRepository personnelQueryRepository) 
    : IRequestHandler<GetAllRescueTeamsQuery, PagedResult<RescueTeamDto>>
{
    public async Task<PagedResult<RescueTeamDto>> Handle(GetAllRescueTeamsQuery request, CancellationToken cancellationToken)
    {
        var pagedModels = await personnelQueryRepository.GetAllRescueTeamsAsync(request.PageNumber, request.PageSize, cancellationToken);

        var dtos = pagedModels.Items.Select(m => new RescueTeamDto
        {
            Id = m.Id,
            Code = m.Code,
            Name = m.Name,
            TeamType = m.TeamType.ToString(),
            Status = m.Status.ToString(),
            AssemblyPointId = m.AssemblyPointId,
            AssemblyPointName = m.AssemblyPointName,
            MaxMembers = m.MaxMembers,
            CurrentMemberCount = m.Members.Count(x => x.Status != Domain.Enum.Personnel.TeamMemberStatus.Removed),
            CreatedAt = m.CreatedAt
        }).ToList();

        return new PagedResult<RescueTeamDto>(dtos, pagedModels.TotalCount, pagedModels.PageNumber, pagedModels.PageSize);
    }
}
