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
            IsCheckedIn = false
        };
    }

    /// <summary>
    /// Check-in rescuer tại sự kiện tập trung. Idempotent — nếu đã check-in thì bỏ qua.
    /// </summary>
    public void CheckIn()
    {
        if (IsCheckedIn) return;

        IsCheckedIn = true;
        CheckInTime = DateTime.UtcNow;
        Status = AssemblyParticipantStatus.CheckedIn;
    }
}
