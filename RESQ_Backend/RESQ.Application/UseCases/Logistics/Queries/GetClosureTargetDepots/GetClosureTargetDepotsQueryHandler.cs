namespace RESQ.Application.UseCases.Logistics.Queries.GetClosureTargetDepots;

using MediatR;
using RESQ.Domain.Enum.Logistics;
using RESQ.Application.Repositories.Logistics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class GetClosureTargetDepotsQueryHandler : IRequestHandler<GetClosureTargetDepotsQuery, List<TargetDepotKeyValueDto>>
{
    private readonly IDepotRepository _depotRepository;

    public GetClosureTargetDepotsQueryHandler(IDepotRepository depotRepository)
    {
        _depotRepository = depotRepository;
    }

    public async Task<List<TargetDepotKeyValueDto>> Handle(GetClosureTargetDepotsQuery request, CancellationToken cancellationToken)
    {
        var allDepots = await _depotRepository.GetAllAsync(cancellationToken);

        var validTargets = allDepots
            .Where(d => d.Status != DepotStatus.Unavailable && 
                        d.Status != DepotStatus.Closing && 
                        d.Status != DepotStatus.Closed)
            .Select(d => new TargetDepotKeyValueDto
            {
                Id = d.Id,
                Name = d.Name
            })
            .ToList();

        return validTargets;
    }
}
