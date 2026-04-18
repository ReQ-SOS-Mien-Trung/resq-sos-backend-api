using RESQ.Domain.Enum.Personnel;

namespace RESQ.Domain.Entities.Personnel;

public class AssemblyParticipantModel
{
    public int Id { get; set; }
    public int AssemblyEventId { get; set; }
    public Guid RescuerId { get; set; }
    public AssemblyParticipantStatus Status { get; set; }
    public bool IsCheckedIn { get; set; }
    public DateTime? CheckInTime { get; set; }
    public bool IsCheckedOut { get; set; }
    public DateTime? CheckOutTime { get; set; }

    /// <summary>Rescuer check-in trước giờ triệu tập.</summary>
    public bool IsEarly => CheckInTime.HasValue && EventStartTime.HasValue && CheckInTime.Value < EventStartTime.Value;

    /// <summary>Rescuer check-in sau giờ triệu tập.</summary>
    public bool IsLate => CheckInTime.HasValue && EventStartTime.HasValue && CheckInTime.Value > EventStartTime.Value;

    /// <summary>Ngày giờ triệu tập (load từ event, không lưu DB).</summary>
    public DateTime? EventStartTime { get; set; }

    public AssemblyParticipantModel() { }

    /// <summary>
    /// Tạo participant mới khi sự kiện được tạo (snapshot rescuer).
    /// </summary>
    public static AssemblyParticipantModel CreateAssigned(int eventId, Guid rescuerId)
    {
        return new AssemblyParticipantModel
        {
            AssemblyEventId = eventId,
            RescuerId = rescuerId,
            Status = AssemblyParticipantStatus.Assigned,
            IsCheckedIn = false,
            IsCheckedOut = false
        };
    }

    /// <summary>
    /// Check-in rescuer tại sự kiện tập trung. Idempotent - nếu đã check-in thì bỏ qua.
    /// </summary>
    public void CheckIn()
    {
        if (IsCheckedIn) return;

        IsCheckedIn = true;
        CheckInTime = DateTime.UtcNow;
        Status = AssemblyParticipantStatus.CheckedIn;
    }

    /// <summary>
    /// Check-out rescuer tại sự kiện tập trung. Chỉ áp dụng nếu đã check-in.
    /// </summary>
    public void CheckOut()
    {
        if (!IsCheckedIn) throw new global::System.InvalidOperationException("Cannot checkout. Rescuer hasn't checked in yet.");
        if (IsCheckedOut) return;

        IsCheckedOut = true;
        CheckOutTime = DateTime.UtcNow;
    }
}
