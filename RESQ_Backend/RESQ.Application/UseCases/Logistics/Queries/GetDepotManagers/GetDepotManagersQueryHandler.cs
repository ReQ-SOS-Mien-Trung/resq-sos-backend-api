using MediatR;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers;

public class GetDepotManagersQueryHandler(IDepotRepository depotRepository)
    : IRequestHandler<GetDepotManagersQuery, List<DepotManagerInfoDto>>
{
    public Task<List<DepotManagerInfoDto>> Handle(GetDepotManagersQuery request, CancellationToken cancellationToken)
        => depotRepository.GetDepotManagersAsync(request.DepotId, cancellationToken);
}
