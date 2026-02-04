namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;

public class AssemblyPointDto
{
    public int Id { get; set; }
    public string? Code { get; set; } // Added
    public string? Name { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int CapacityTeams { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastUpdatedAt { get; set; }
}
