namespace RESQ.Application.UseCases.Personnel.Commands.CreateAssemblyPoint;

public class CreateAssemblyPointRequestDto
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int MaxCapacity { get; set; }
    /// <summary>URL ảnh đại diện điểm tập kết (tuỳ chọn).</summary>
    public string? ImageUrl { get; set; }
}
