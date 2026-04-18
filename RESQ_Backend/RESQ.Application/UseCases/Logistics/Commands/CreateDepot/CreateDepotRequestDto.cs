namespace RESQ.Application.UseCases.Logistics.Commands.CreateDepot;

public class CreateDepotRequestDto
{
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal Capacity { get; set; }
    /// <summary>Sức chứa tối đa theo cân nặng (kg).</summary>
    public decimal WeightCapacity { get; set; }
    /// <summary>Optional: gán manager ngay khi tạo kho. Nếu không có, kho ở trạng thái PendingAssignment.</summary>
    public Guid? ManagerId { get; set; }
    /// <summary>URL ảnh đại diện kho (tuỳ chọn).</summary>
    public string? ImageUrl { get; set; }
}
