using MediatR;
using RESQ.Application.Common.Logistics;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetSupplyRequestPriorityConfig;

public class GetSupplyRequestPriorityConfigQueryHandler(
    ISupplyRequestPriorityConfigRepository supplyRequestPriorityConfigRepository)
    : IRequestHandler<GetSupplyRequestPriorityConfigQuery, GetSupplyRequestPriorityConfigResponse>
{
    private readonly ISupplyRequestPriorityConfigRepository _supplyRequestPriorityConfigRepository = supplyRequestPriorityConfigRepository;

    public async Task<GetSupplyRequestPriorityConfigResponse> Handle(
        GetSupplyRequestPriorityConfigQuery request,
        CancellationToken cancellationToken)
    {
        var config = await _supplyRequestPriorityConfigRepository.GetAsync(cancellationToken);
        var timing = config == null
            ? SupplyRequestPriorityPolicy.DefaultTiming
            : new SupplyRequestPriorityTiming(config.UrgentMinutes, config.HighMinutes, config.MediumMinutes);

        return new GetSupplyRequestPriorityConfigResponse
        {
            UrgentMinutes = timing.UrgentMinutes,
            HighMinutes = timing.HighMinutes,
            MediumMinutes = timing.MediumMinutes,
            UpdatedBy = config?.UpdatedBy,
            UpdatedAt = config?.UpdatedAt ?? DateTime.UtcNow
        };
    }
}
