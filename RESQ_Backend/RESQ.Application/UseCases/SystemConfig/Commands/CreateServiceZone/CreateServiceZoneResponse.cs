using RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateServiceZone;

public class CreateServiceZoneResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<CoordinatePointDto> Coordinates { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
