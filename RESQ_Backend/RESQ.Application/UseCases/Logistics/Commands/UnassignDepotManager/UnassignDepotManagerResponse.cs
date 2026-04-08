namespace RESQ.Application.UseCases.Logistics.Commands.UnassignDepotManager;

public class UnassignDepotManagerResponse
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime UnassignedAt { get; set; }
}
