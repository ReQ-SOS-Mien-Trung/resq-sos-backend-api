namespace RESQ.Application.UseCases.Logistics.Commands.UpsertSupplyRequestPriorityConfig;

public class UpsertSupplyRequestPriorityConfigResponse
{
    public int UrgentMinutes { get; set; }
    public int HighMinutes { get; set; }
    public int MediumMinutes { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
