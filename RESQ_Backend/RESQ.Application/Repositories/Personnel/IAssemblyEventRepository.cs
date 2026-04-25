using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyEvents;
using RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers;
using RESQ.Application.UseCases.Personnel.Queries.GetMyAssemblyEvents;
using RESQ.Application.UseCases.Personnel.Queries.GetMyUpcomingAssemblyEvents;

namespace RESQ.Application.Repositories.Personnel;

public interface IAssemblyEventRepository
{
    /// <summary>Tạo sự kiện tập trung mới. Trả về event ID.</summary>
    Task<int> CreateEventAsync(int assemblyPointId, DateTime assemblyDate, DateTime checkInDeadline, Guid createdBy, CancellationToken cancellationToken = default);

    /// <summary>Snapshot: gán danh sách rescuer vào sự kiện (chỉ thêm, không xóa).</summary>
    Task AssignParticipantsAsync(int eventId, List<Guid> rescuerIds, CancellationToken cancellationToken = default);

    /// <summary>Check-in rescuer tại sự kiện. Trả về false nếu không tìm thấy participant.</summary>
    Task<bool> CheckInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check-out do hệ thống tự động khi đội xuất phát làm nhiệm vụ (Status = CheckedOutForMission).
    /// Rescuer có thể trở về và check-in lại qua ReturnCheckInAsync.
    /// </summary>
    Task<bool> CheckOutAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check-out do rescuer tự rời sự kiện tập trung (Status = CheckedOut).
    /// Rescuer KHÔNG thể check-in lại sau thao tác này.
    /// </summary>
    Task<bool> CheckOutVoluntaryAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default);

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

    /// <summary>Lấy event active (Gathering) mới nhất tại AP. Null nếu không có.</summary>
    Task<(int EventId, string Status)?> GetActiveEventByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default);
    /// <summary>Lấy event gần nhất tại AP để đối chiếu danh sách participant/check-in khi tạo team.</summary>
    Task<(int EventId, string Status)?> GetLatestEventByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default);

    /// <summary>Cập nhật trạng thái event.</summary>
    Task UpdateEventStatusAsync(int eventId, string status, CancellationToken cancellationToken = default);

    Task<List<Guid>> GetParticipantIdsAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>Lấy event theo ID. Null nếu không tồn tại.</summary>
    Task<(int EventId, int AssemblyPointId, string Status, DateTime AssemblyDate, DateTime? CheckInDeadline)?> GetEventByIdAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>Lấy userId của người tạo event (coordinator). Null nếu event không tồn tại.</summary>
    Task<Guid?> GetEventCreatedByAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra rescuer đã check-out tại sự kiện (không thể check-in lại sau đó).</summary>
    Task<bool> HasParticipantCheckedOutAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu participant là vắng mặt (Absent) tại sự kiện tập trung.
    /// Nếu participant đang checked-in thì đồng thời đánh dấu checked-out.
    /// Trả về false nếu không tìm thấy participant.
    /// </summary>
    Task<bool> MarkParticipantAbsentAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách sự kiện triệu tập mà rescuer được gán vào (phân trang, mới nhất trước).</summary>
    Task<PagedResult<MyAssemblyEventDto>> GetAssemblyEventsForRescuerAsync(
        Guid rescuerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách sự kiện sắp tới (Gathering) mà rescuer được gán,
    /// sắp xếp theo thời gian triệu tập tăng dần (gần nhất trước).
    /// </summary>
    Task<List<UpcomingAssemblyEventDto>> GetUpcomingEventsForRescuerAsync(
        Guid rescuerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách ID sự kiện (Gathering) đã quá CheckInDeadline mà vẫn còn participant chưa check-in.
    /// Dùng cho background service tự động đánh dấu vắng mặt.
    /// </summary>
    Task<List<int>> GetGatheringEventsWithExpiredDeadlineAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách ID sự kiện Gathering đã quá CheckInDeadline (bất kể participant đã xử lý xong hay chưa).
    /// Dùng cho background service tự động chuyển sang Completed.
    /// </summary>
    Task<List<int>> GetGatheringEventsExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>Chuyển trạng thái sự kiện sang Completed. Idempotent.</summary>
    Task CompleteEventAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tự động đánh dấu Absent tất cả participant trong sự kiện chưa check-in (IsCheckedIn = false).
    /// Trả về số lượng participant bị đánh dấu.
    /// </summary>
    Task<int> AutoMarkAbsentForEventAsync(int eventId, CancellationToken cancellationToken = default);
}
