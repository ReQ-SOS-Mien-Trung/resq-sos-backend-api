using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetRescuers;

public class GetRescuersQueryHandler(IPersonnelQueryRepository personnelQueryRepository)
    : IRequestHandler<GetRescuersQuery, PagedResult<RescuerDto>>
{
    public async Task<PagedResult<RescuerDto>> Handle(
        GetRescuersQuery request,
        CancellationToken cancellationToken)
    {
        var pagedModels = await personnelQueryRepository.GetRescuersAsync(
            request.PageNumber,
            request.PageSize,
            request.HasAssemblyPoint,
            request.HasTeam,
            request.RescuerType,
            request.AbilitySubgroupCode,
            request.AbilityCategoryCode,
            request.FirstName,
            request.LastName,
            request.Email,
            cancellationToken);

        var dtos = pagedModels.Items.Select(m => new RescuerDto
        {
            Id = m.Id,
            FirstName = m.FirstName,
            LastName = m.LastName,
            Phone = m.Phone,
            Email = m.Email,
            AvatarUrl = m.AvatarUrl,
            RescuerType = m.RescuerType,
            Address = m.Address,
            Ward = m.Ward,
            Province = m.Province,
            HasTeam = m.HasTeam,
            HasAssemblyPoint = m.HasAssemblyPoint,
            TopAbilities = m.TopAbilities
        }).ToList();

        return new PagedResult<RescuerDto>(
            dtos,
            pagedModels.TotalCount,
            pagedModels.PageNumber,
            pagedModels.PageSize);
    }
}
