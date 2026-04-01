using MediatR;
using RESQ.Application.UseCases.Logistics.Thresholds;

namespace RESQ.Application.UseCases.Logistics.Commands.UpsertWarningBandConfig;

public class UpsertWarningBandConfigCommand : IRequest<WarningBandConfigResponse>
{
    public Guid UserId { get; set; }
    public UpsertWarningBandRequest Request { get; set; } = new();
}
