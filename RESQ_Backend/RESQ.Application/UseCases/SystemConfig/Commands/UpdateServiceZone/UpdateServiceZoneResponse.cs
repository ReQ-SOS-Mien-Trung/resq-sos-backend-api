namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;

public class UpdateServiceZoneResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<CoordinatePointDto> Coordinates { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}
