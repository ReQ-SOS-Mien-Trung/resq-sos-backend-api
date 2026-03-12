using RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetServiceZone;

public class GetServiceZoneResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<CoordinatePointDto> Coordinates { get; set; } = new();
    public bool IsActive { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
