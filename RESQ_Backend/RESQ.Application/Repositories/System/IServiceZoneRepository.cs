using RESQ.Domain.Entities.System;

namespace RESQ.Application.Repositories.System;

public interface IServiceZoneRepository
{
    Task<ServiceZoneModel?> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<List<ServiceZoneModel>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<ServiceZoneModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<ServiceZoneModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(ServiceZoneModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(ServiceZoneModel model, CancellationToken cancellationToken = default);
    Task DeactivateAllExceptAsync(int excludeId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Kiểm tra xem tọa độ có nằm trong ít nhất một vùng phục vụ đang active hay không.
    /// Trả về true nếu không có vùng nào được cấu hình (không giới hạn).
    /// </summary>
    Task<bool> IsLocationInServiceZoneAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
