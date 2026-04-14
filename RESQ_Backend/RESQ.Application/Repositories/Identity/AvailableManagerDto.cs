namespace RESQ.Application.Repositories.Identity;

public class AvailableManagerDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    /// <summary>Số lượng kho hiện tại người này đang quản lý (UnassignedAt == null).</summary>
    public int AssignedDepotsCount { get; set; }
}
