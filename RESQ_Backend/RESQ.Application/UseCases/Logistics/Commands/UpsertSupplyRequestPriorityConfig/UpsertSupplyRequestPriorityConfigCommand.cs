using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.UpsertSupplyRequestPriorityConfig;

public class UpsertSupplyRequestPriorityConfigCommand : IRequest<UpsertSupplyRequestPriorityConfigResponse>
{
    public Guid UserId { get; set; }
    public int UrgentMinutes { get; set; }
    public int HighMinutes { get; set; }
    public int MediumMinutes { get; set; }
}
