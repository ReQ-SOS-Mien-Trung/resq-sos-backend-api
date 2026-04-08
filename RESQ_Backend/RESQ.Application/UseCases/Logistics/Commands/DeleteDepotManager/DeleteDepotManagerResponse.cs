namespace RESQ.Application.UseCases.Logistics.Commands.DeleteDepotManager;

public class DeleteDepotManagerResponse
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; }
}
