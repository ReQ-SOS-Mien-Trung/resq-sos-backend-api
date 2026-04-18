using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertSosClusterGroupingConfig;

public class UpsertSosClusterGroupingConfigCommand : IRequest<UpsertSosClusterGroupingConfigResponse>
{
    public Guid UserId { get; set; }
    public double MaximumDistanceKm { get; set; }
}
