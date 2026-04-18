using MediatR;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Commands.UpsertWarningBandConfig;
using RESQ.Application.UseCases.Logistics.Thresholds;

namespace RESQ.Application.UseCases.Logistics.Queries.GetWarningBandConfig;

public class GetWarningBandConfigHandler(IStockWarningBandConfigRepository repo)
    : IRequestHandler<GetWarningBandConfigQuery, WarningBandConfigResponse?>
{
    private readonly IStockWarningBandConfigRepository _repo = repo;

    public async Task<WarningBandConfigResponse?> Handle(GetWarningBandConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await _repo.GetAsync(cancellationToken);
        return config == null ? null : UpsertWarningBandConfigCommandHandler.ToResponse(config);
    }
}
