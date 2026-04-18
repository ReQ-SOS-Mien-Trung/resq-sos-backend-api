using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyEvents;

public class GetAssemblyEventsQueryHandler(
    IAssemblyPointRepository assemblyPointRepository,
    IAssemblyEventRepository assemblyEventRepository)
    : IRequestHandler<GetAssemblyEventsQuery, PagedResult<AssemblyEventListItemDto>>
{
    public async Task<PagedResult<AssemblyEventListItemDto>> Handle(
        GetAssemblyEventsQuery request,
        CancellationToken cancellationToken)
    {
        _ = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết với id = {request.AssemblyPointId}");

        return await assemblyEventRepository.GetEventsByAssemblyPointAsync(
            request.AssemblyPointId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
