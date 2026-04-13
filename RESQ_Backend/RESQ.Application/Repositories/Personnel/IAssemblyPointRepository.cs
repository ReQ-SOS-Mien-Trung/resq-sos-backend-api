using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;
using RESQ.Domain.Entities.Personnel;

namespace RESQ.Application.Repositories.Personnel;

public interface IAssemblyPointRepository
{
    Task CreateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    
    Task<AssemblyPointModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<AssemblyPointModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<AssemblyPointModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    
    Task<PagedResult<AssemblyPointModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy tất cả điểm tập kết (không phân trang) - dùng cho metadata dropdown.
    /// </summary>
    Task<List<AssemblyPointModel>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách đội cứu hộ (kèm thành viên) được gán vào các điểm tập kết.
    /// Key = AssemblyPointId.
    /// </summary>
    Task<Dictionary<int, List<AssemblyPointTeamDto>>> GetTeamsByAssemblyPointIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách user ID của rescuer được gán vào điểm tập kết (User.AssemblyPointId).</summary>
    Task<List<Guid>> GetAssignedRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách rescuer tại điểm tập kết mà CHƯA thuộc bất kỳ đội cứu hộ nào đang hoạt động.
    /// Dùng cho luồng triệu tập - chỉ triệu tập rescuer chưa có team để xếp nhóm.
    /// </summary>
    Task<List<Guid>> GetTeamlessRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra rescuer có đang thuộc đội cứu hộ hoạt động nào không.</summary>
    Task<bool> HasActiveTeamAsync(Guid rescuerUserId, CancellationToken cancellationToken = default);

    /// <summary>Gán hoặc thay đổi điểm tập kết cho rescuer. Truyền null để gỡ.</summary>
    Task UpdateRescuerAssemblyPointAsync(Guid rescuerUserId, int? assemblyPointId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gán hoặc thay đổi điểm tập kết cho nhiều rescuer cùng lúc (single bulk UPDATE).
    /// Trả về danh sách UserId thực sự được cập nhật (tồn tại trong DB và có roleId = 3).
    /// </summary>
    Task<List<Guid>> BulkUpdateRescuerAssemblyPointAsync(IReadOnlyList<Guid> userIds, int? assemblyPointId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trong tập <paramref name="userIds"/>, trả về các UserId KHÔNG thuộc đội cứu hộ đang hoạt động.
    /// </summary>
    Task<List<Guid>> FilterUsersWithoutActiveTeamAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken = default);
}
