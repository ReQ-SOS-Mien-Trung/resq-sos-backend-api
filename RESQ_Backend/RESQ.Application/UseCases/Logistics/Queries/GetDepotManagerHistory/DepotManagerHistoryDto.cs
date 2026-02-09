namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotManagerHistory;

public class DepotManagerHistoryDto
{
    public Guid UserId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
    public bool IsCurrent { get; set; }
}
