using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetRescuersByAssemblyPoint;

public class GetRescuersByAssemblyPointQueryHandler(IPersonnelQueryRepository personnelQueryRepository)
    : IRequestHandler<GetRescuersByAssemblyPointQuery, PagedResult<RescuerByAssemblyPointDto>>
{
    public async Task<PagedResult<RescuerByAssemblyPointDto>> Handle(
        GetRescuersByAssemblyPointQuery request,
        CancellationToken cancellationToken)
    {
        var pagedModels = await personnelQueryRepository.GetRescuersByAssemblyPointAsync(
            request.AssemblyPointId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = pagedModels.Items.Select(m => new RescuerByAssemblyPointDto
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
            Province = m.Province
        }).ToList();

        return new PagedResult<RescuerByAssemblyPointDto>(
            dtos,
            pagedModels.TotalCount,
            pagedModels.PageNumber,
            pagedModels.PageSize);
    }
}
