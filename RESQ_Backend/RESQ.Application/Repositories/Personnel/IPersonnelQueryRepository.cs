using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.Repositories.Personnel;

public interface IPersonnelQueryRepository
{
    Task<PagedResult<FreeRescuerModel>> GetFreeRescuersAsync(
        int pageNumber, int pageSize,
        string? firstName = null, string? lastName = null,
        string? phone = null, string? email = null,
        RescuerType? rescuerType = null,
        CancellationToken cancellationToken = default);
    Task<PagedResult<RescueTeamModel>> GetAllRescueTeamsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<RescueTeamModel?> GetRescueTeamDetailAsync(int teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy đội cứu hộ "active" mà user hiện tại đang thuộc về (Accepted + team chưa Disbanded).
    /// </summary>
    Task<RescueTeamModel?> GetActiveRescueTeamByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách rescuer (đã Accepted) thuộc các đội tại một điểm tập kết.
    /// </summary>
    Task<PagedResult<FreeRescuerModel>> GetRescuersByAssemblyPointAsync(
        int assemblyPointId,
        int pageNumber, int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy toàn bộ đội cứu hộ có trạng thái <c>Available</c> (không phân trang) — dùng khi cần so sánh khoảng cách để gán nhiệm vụ.
    /// </summary>
    Task<List<RescueTeamModel>> GetAllAvailableTeamsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách rescuer với các filter tuỳ chọn:
    /// hasAssemblyPoint, hasTeam, rescuerType, abilitySubgroupCode, abilityCategoryCode.
    /// <para><paramref name="search"/>: tìm đồng thời theo firstName, lastName, phone hoặc email (OR).</para>
    /// </summary>
    Task<PagedResult<RescuerModel>> GetRescuersAsync(
        int pageNumber, int pageSize,
        bool? hasAssemblyPoint = null,
        bool? hasTeam = null,
        RescuerType? rescuerType = null,
        string? abilitySubgroupCode = null,
        string? abilityCategoryCode = null,
        string? search = null,
        List<string>? assemblyPointCodes = null,
        CancellationToken cancellationToken = default);
}
