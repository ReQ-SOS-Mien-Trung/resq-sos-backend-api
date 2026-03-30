using MediatR;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Thresholds;

namespace RESQ.Application.UseCases.Logistics.Queries.GetWarningBandConfig;

public class GetWarningBandConfigHandler(IStockWarningBandConfigRepository repo)
    : IRequestHandler<GetWarningBandConfigQuery, WarningBandConfigDto?>
{
    private readonly IStockWarningBandConfigRepository _repo = repo;

    public Task<WarningBandConfigDto?> Handle(GetWarningBandConfigQuery request, CancellationToken cancellationToken)
        => _repo.GetAsync(cancellationToken);
}
