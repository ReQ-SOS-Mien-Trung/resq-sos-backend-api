using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetMyAssemblyEvents;

public class GetMyAssemblyEventsQueryHandler(
    IAssemblyEventRepository assemblyEventRepository)
    : IRequestHandler<GetMyAssemblyEventsQuery, PagedResult<MyAssemblyEventDto>>
{
    public async Task<PagedResult<MyAssemblyEventDto>> Handle(
        GetMyAssemblyEventsQuery request,
        CancellationToken cancellationToken)
    {
        return await assemblyEventRepository.GetAssemblyEventsForRescuerAsync(
            request.RescuerId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
