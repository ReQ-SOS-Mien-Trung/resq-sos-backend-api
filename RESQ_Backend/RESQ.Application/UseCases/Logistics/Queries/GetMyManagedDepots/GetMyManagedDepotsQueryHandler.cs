using MediatR;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyManagedDepots;

public class GetMyManagedDepotsQueryHandler(IManagerDepotAccessService managerDepotAccessService)
    : IRequestHandler<GetMyManagedDepotsQuery, List<ManagedDepotDto>>
{
    public Task<List<ManagedDepotDto>> Handle(GetMyManagedDepotsQuery request, CancellationToken cancellationToken)
        => managerDepotAccessService.GetManagedDepotsAsync(request.UserId, cancellationToken);
}
