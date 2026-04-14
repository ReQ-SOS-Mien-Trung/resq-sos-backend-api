using MediatR;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetSosClusterGroupingConfig;

public class GetSosClusterGroupingConfigQueryHandler(
    ISosClusterGroupingConfigRepository sosClusterGroupingConfigRepository)
    : IRequestHandler<GetSosClusterGroupingConfigQuery, GetSosClusterGroupingConfigResponse>
{
    private const double DefaultMaximumDistanceKm = 10.0;

    private readonly ISosClusterGroupingConfigRepository _sosClusterGroupingConfigRepository = sosClusterGroupingConfigRepository;

    public async Task<GetSosClusterGroupingConfigResponse> Handle(
        GetSosClusterGroupingConfigQuery request,
        CancellationToken cancellationToken)
    {
        var config = await _sosClusterGroupingConfigRepository.GetAsync(cancellationToken);

        return new GetSosClusterGroupingConfigResponse
        {
            MaximumDistanceKm = config?.MaximumDistanceKm ?? DefaultMaximumDistanceKm,
            UpdatedBy = config?.UpdatedBy,
            UpdatedAt = config?.UpdatedAt ?? DateTime.UtcNow
        };
    }
}
