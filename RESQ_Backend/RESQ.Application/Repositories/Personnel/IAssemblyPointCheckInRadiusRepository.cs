namespace RESQ.Application.Repositories.Personnel;

public class AssemblyPointCheckInRadiusConfigDto
{
    public int Id { get; set; }
    public int AssemblyPointId { get; set; }
    public double MaxRadiusMeters { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public interface IAssemblyPointCheckInRadiusRepository
{
    /// <summary>Lấy cấu hình riêng của 1 điểm tập kết. Trả về null nếu chưa có.</summary>
    Task<AssemblyPointCheckInRadiusConfigDto?> GetByAssemblyPointIdAsync(int assemblyPointId, CancellationToken cancellationToken = default);

    /// <summary>Tạo mới hoặc cập nhật cấu hình bán kính cho điểm tập kết.</summary>
    Task<AssemblyPointCheckInRadiusConfigDto> UpsertAsync(int assemblyPointId, double maxRadiusMeters, Guid updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Lấy tất cả cấu hình bán kính check-in riêng đang được thiết lập.</summary>
    Task<List<AssemblyPointCheckInRadiusConfigDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Xóa cấu hình riêng; điểm tập kết sẽ quay về dùng cấu hình toàn cục.</summary>
    /// <returns>true nếu xóa thành công; false nếu không tìm thấy.</returns>
    Task<bool> DeleteByAssemblyPointIdAsync(int assemblyPointId, CancellationToken cancellationToken = default);
}
