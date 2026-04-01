using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers;

public class GetCheckedInRescuersQueryHandler(
    IAssemblyEventRepository assemblyEventRepository)
    : IRequestHandler<GetCheckedInRescuersQuery, PagedResult<CheckedInRescuerDto>>
{
    public async Task<PagedResult<CheckedInRescuerDto>> Handle(
        GetCheckedInRescuersQuery request, CancellationToken cancellationToken)
    {
        _ = await assemblyEventRepository.GetEventByIdAsync(request.AssemblyEventId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy sự kiện tập trung id = {request.AssemblyEventId}");

        return await assemblyEventRepository.GetCheckedInRescuersAsync(
            request.AssemblyEventId,
            request.PageNumber,
            request.PageSize,
            request.RescuerType,
            request.AbilitySubgroupCode,
            request.AbilityCategoryCode,
            request.Search,
            cancellationToken);
    }
}
