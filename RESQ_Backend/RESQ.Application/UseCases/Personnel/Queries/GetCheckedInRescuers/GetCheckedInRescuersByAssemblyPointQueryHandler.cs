using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers;

public class GetCheckedInRescuersByAssemblyPointQueryHandler(
    IAssemblyEventRepository assemblyEventRepository)
    : IRequestHandler<GetCheckedInRescuersByAssemblyPointQuery, PagedResult<CheckedInRescuerDto>>
{
    public async Task<PagedResult<CheckedInRescuerDto>> Handle(
        GetCheckedInRescuersByAssemblyPointQuery request,
        CancellationToken cancellationToken)
    {
        return await assemblyEventRepository.GetCheckedInRescuersByAssemblyPointAsync(
            request.AssemblyPointId,
            request.PageNumber,
            request.PageSize,
            request.RescuerType,
            request.AbilitySubgroupCode,
            request.AbilityCategoryCode,
            request.Search,
            cancellationToken);
    }
}
