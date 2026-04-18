namespace RESQ.Application.UseCases.Logistics.Commands.UpsertSupplyRequestPriorityConfig;

public class UpsertSupplyRequestPriorityConfigRequest
{
    public int UrgentMinutes { get; set; }
    public int HighMinutes { get; set; }
    public int MediumMinutes { get; set; }
}
