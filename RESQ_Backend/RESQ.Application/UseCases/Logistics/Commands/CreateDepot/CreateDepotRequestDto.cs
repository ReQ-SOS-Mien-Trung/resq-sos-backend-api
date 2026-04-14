namespace RESQ.Application.UseCases.Logistics.Commands.CreateDepot;

public class CreateDepotRequestDto
{
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal Capacity { get; set; }
    /// <summary>S?c ch?a t?i da theo cân n?ng (kg).</summary>
    public decimal WeightCapacity { get; set; }
    /// <summary>Optional: gán manager ngay khi t?o kho. N?u không có, kho ? tr?ng thái PendingAssignment.</summary>
    public Guid? ManagerId { get; set; }
    /// <summary>URL ?nh d?i di?n kho (tu? ch?n).</summary>
    public string? ImageUrl { get; set; }
}
