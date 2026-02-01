namespace RESQ.Application.UseCases.Logistics.Commands.UpdateDepot;

public class UpdateDepotRequestDto
{
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Capacity { get; set; }
}
