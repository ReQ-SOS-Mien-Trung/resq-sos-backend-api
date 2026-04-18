using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetFreeRescuers;

public class GetFreeRescuersQueryHandler(IPersonnelQueryRepository personnelQueryRepository) 
    : IRequestHandler<GetFreeRescuersQuery, PagedResult<FreeRescuerDto>>
{
    public async Task<PagedResult<FreeRescuerDto>> Handle(GetFreeRescuersQuery request, CancellationToken cancellationToken)
    {
        var pagedModels = await personnelQueryRepository.GetFreeRescuersAsync(
            request.PageNumber, request.PageSize,
            request.FirstName, request.LastName, request.Phone, request.Email, request.RescuerType,
            cancellationToken);

        // Map Domain Model to DTO in Application Layer
        var dtos = pagedModels.Items.Select(m => new FreeRescuerDto
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
            TopAbilities = m.TopAbilities
        }).ToList();

        return new PagedResult<FreeRescuerDto>(dtos, pagedModels.TotalCount, pagedModels.PageNumber, pagedModels.PageSize);
    }
}
