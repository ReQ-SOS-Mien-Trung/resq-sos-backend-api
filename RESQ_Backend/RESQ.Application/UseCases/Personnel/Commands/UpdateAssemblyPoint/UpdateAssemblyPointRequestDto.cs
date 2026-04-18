namespace RESQ.Application.UseCases.Personnel.Commands.UpdateAssemblyPoint;

public class UpdateAssemblyPointRequestDto
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int MaxCapacity { get; set; }
    /// <summary>URL ảnh đại diện điểm tập kết (tuỳ chọn). Truyền null để giữ nguyên ảnh cũ.</summary>
    public string? ImageUrl { get; set; }
}
