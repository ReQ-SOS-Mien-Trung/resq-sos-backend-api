namespace RESQ.Application.UseCases.Logistics.Commands.AssignDepotManager;

public class AssignDepotManagerResponse
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<AssignedManagerInfo> AssignedManagers { get; set; } = [];
    public int AssignedCount => AssignedManagers.Count;
}

public class AssignedManagerInfo
{
    public Guid ManagerId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public DateTime AssignedAt { get; set; }
}
