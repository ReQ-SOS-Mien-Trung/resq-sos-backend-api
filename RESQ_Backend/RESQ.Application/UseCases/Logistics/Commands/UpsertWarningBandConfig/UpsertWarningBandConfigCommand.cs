using MediatR;
using RESQ.Application.UseCases.Logistics.Thresholds;

namespace RESQ.Application.UseCases.Logistics.Commands.UpsertWarningBandConfig;

public class UpsertWarningBandConfigCommand : IRequest<WarningBandConfigDto>
{
    public Guid UserId { get; set; }
    public List<WarningBandDto> Bands { get; set; } = [];
}
