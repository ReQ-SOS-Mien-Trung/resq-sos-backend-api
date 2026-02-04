namespace RESQ.Application.UseCases.Personnel.Commands.UpdateAssemblyPoint;

public class UpdateAssemblyPointRequestDto
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int CapacityTeams { get; set; }
}