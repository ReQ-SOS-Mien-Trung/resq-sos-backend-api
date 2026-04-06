namespace RESQ.Application.UseCases.Logistics.Commands.AssignDepotManager;

public class AssignDepotManagerResponse
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid ManagerId { get; set; }
    public string? ManagerFullName { get; set; }
    public string? ManagerEmail { get; set; }
    public DateTime AssignedAt { get; set; }
}
