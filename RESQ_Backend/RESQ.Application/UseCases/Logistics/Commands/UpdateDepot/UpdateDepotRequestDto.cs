namespace RESQ.Application.UseCases.Logistics.Commands.UpdateDepot;

public class UpdateDepotRequestDto
{
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal Capacity { get; set; }
    /// <summary>Sức chứa tối đa theo cân nặng (kg).</summary>
    public decimal WeightCapacity { get; set; }
    /// <summary>URL ảnh đại diện kho (tuỳ chọn). Truyền null để giữ nguyên ảnh cũ.</summary>
    public string? ImageUrl { get; set; }
}
