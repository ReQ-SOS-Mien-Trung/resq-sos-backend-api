namespace RESQ.Application.UseCases.Logistics.Queries.GetSupplyRequestPriorityConfig;

public class GetSupplyRequestPriorityConfigResponse
{
    public int UrgentMinutes { get; set; }
    public int HighMinutes { get; set; }
    public int MediumMinutes { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
