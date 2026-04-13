using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyEvents;
using RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers;
using RESQ.Application.UseCases.Personnel.Queries.GetMyAssemblyEvents;

namespace RESQ.Application.Repositories.Personnel;

public interface IAssemblyEventRepository
{
    /// <summary>Tạo sự kiện tập trung mới. Trả về event ID.</summary>
    Task<int> CreateEventAsync(int assemblyPointId, DateTime assemblyDate, Guid createdBy, CancellationToken cancellationToken = default);

    /// <summary>Snapshot: gán danh sách rescuer vào sự kiện (chỉ thêm, không xóa).</summary>
    Task AssignParticipantsAsync(int eventId, List<Guid> rescuerIds, CancellationToken cancellationToken = default);

    /// <summary>Check-in rescuer tại sự kiện. Trả về false nếu không tìm thấy participant.</summary>
    Task<bool> CheckInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default);
    Task<bool> CheckOutAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check-in rescuer khi trở về điểm tập kết sau nhiệm vụ.
    /// Khác <see cref="CheckInAsync"/>: nếu participant đã checkout thì reset IsCheckedOut về false.
    /// Tự động thêm rescuer vào participant nếu chưa có. Trả về false nếu event không tồn tại.
    /// </summary>
    Task<bool> ReturnCheckInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra rescuer đã check-in tại sự kiện chưa.</summary>
    Task<bool> IsParticipantCheckedInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách rescuer đã check-in tại sự kiện (phân trang).
    /// <para><paramref name="search"/>: tìm đồng thời theo firstName, lastName, phone hoặc email (OR).</para>
    /// </summary>
    Task<PagedResult<CheckedInRescuerDto>> GetCheckedInRescuersAsync(
        int eventId, int pageNumber, int pageSize,
        RESQ.Domain.Enum.Identity.RescuerType? rescuerType = null,
        string? abilitySubgroupCode = null,
        string? abilityCategoryCode = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách sự kiện tập trung của một điểm tập kết (phân trang).</summary>
    Task<PagedResult<AssemblyEventListItemDto>> GetEventsByAssemblyPointAsync(int assemblyPointId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Lấy event active (Scheduled/Gathering) mới nhất tại AP. Null nếu không có.</summary>
    Task<(int EventId, string Status)?> GetActiveEventByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default);

    /// <summary>Cập nhật trạng thái event.</summary>
    Task UpdateEventStatusAsync(int eventId, string status, CancellationToken cancellationToken = default);

    Task<List<Guid>> GetParticipantIdsAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>Chuyển trạng thái Scheduled → Gathering.</summary>
    Task StartGatheringAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>Lấy event theo ID. Null nếu không tồn tại.</summary>
    Task<(int EventId, int AssemblyPointId, string Status, DateTime AssemblyDate)?> GetEventByIdAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách sự kiện triệu tập mà rescuer được gán vào (phân trang, mới nhất trước).</summary>
    Task<PagedResult<MyAssemblyEventDto>> GetAssemblyEventsForRescuerAsync(
        Guid rescuerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
