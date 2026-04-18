using MediatR;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetMyUpcomingAssemblyEvents;

public class GetMyUpcomingAssemblyEventsQueryHandler(
    IAssemblyEventRepository assemblyEventRepository)
    : IRequestHandler<GetMyUpcomingAssemblyEventsQuery, List<UpcomingAssemblyEventDto>>
{
    public async Task<List<UpcomingAssemblyEventDto>> Handle(
        GetMyUpcomingAssemblyEventsQuery request,
        CancellationToken cancellationToken)
    {
        return await assemblyEventRepository.GetUpcomingEventsForRescuerAsync(
            request.RescuerId,
            cancellationToken);
    }
}
