namespace RESQ.Application.UseCases.Logistics.Queries.GetAllDepots.Depot;

public class ManagerDto
{
    public Guid Id { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}