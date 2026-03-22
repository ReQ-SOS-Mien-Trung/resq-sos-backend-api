using RESQ.Domain.Entities.Personnel.Exceptions;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Domain.Entities.Personnel;

public class AssemblyEventModel
{
    public int Id { get; set; }
    public int AssemblyPointId { get; set; }
    public DateTime AssemblyDate { get; set; }
    public AssemblyEventStatus Status { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<AssemblyParticipantModel> Participants { get; set; } = [];

    public AssemblyEventModel() { }

    /// <summary>
    /// Tạo sự kiện tập trung mới ở trạng thái Scheduled.
    /// </summary>
    public static AssemblyEventModel Create(int assemblyPointId, DateTime assemblyDate, Guid createdBy)
    {
        return new AssemblyEventModel
        {
            AssemblyPointId = assemblyPointId,
            AssemblyDate = assemblyDate,
            Status = AssemblyEventStatus.Scheduled,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Chuyển trạng thái Scheduled → Gathering (mở check-in).
    /// </summary>
    public void StartGathering()
    {
        if (Status != AssemblyEventStatus.Scheduled)
            throw new InvalidAssemblyEventStatusException(
                $"Không thể bắt đầu tập trung. Trạng thái hiện tại: {Status}. Yêu cầu: Scheduled.");

        Status = AssemblyEventStatus.Gathering;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Chuyển trạng thái Gathering → Completed (đóng sự kiện).
    /// </summary>
    public void Complete()
    {
        if (Status != AssemblyEventStatus.Gathering)
            throw new InvalidAssemblyEventStatusException(
                $"Không thể hoàn tất sự kiện. Trạng thái hiện tại: {Status}. Yêu cầu: Gathering.");

        Status = AssemblyEventStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }
}
