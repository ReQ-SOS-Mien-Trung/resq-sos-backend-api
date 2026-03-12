using RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateServiceZone;

public class CreateServiceZoneRequestDto
{
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Danh sách đỉnh polygon vẽ trên bản đồ. Tối thiểu 3 điểm, không cần điểm đóng cuối.
    /// </summary>
    public List<CoordinatePointDto> Coordinates { get; set; } = new();
    public bool IsActive { get; set; } = true;
}
