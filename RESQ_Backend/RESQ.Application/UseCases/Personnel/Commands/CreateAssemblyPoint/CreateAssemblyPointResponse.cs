namespace RESQ.Application.UseCases.Personnel.Commands.CreateAssemblyPoint;

public class CreateAssemblyPointResponse
{
    public int Id { get; set; }
    public string? Code { get; set; } // Added
    public string? Name { get; set; }
    public int CapacityTeams { get; set; }
    public string Status { get; set; } = string.Empty;
}
