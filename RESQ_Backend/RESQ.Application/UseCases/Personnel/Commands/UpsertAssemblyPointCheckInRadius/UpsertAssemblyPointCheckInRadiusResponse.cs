namespace RESQ.Application.UseCases.Personnel.Commands.UpsertAssemblyPointCheckInRadius;

public class UpsertAssemblyPointCheckInRadiusResponse
{
    public int AssemblyPointId { get; set; }
    public double MaxRadiusMeters { get; set; }
    public DateTime UpdatedAt { get; set; }
}
