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
    /// Tạo sự kiện tập trung mới ở trạng thái Gathering.
    /// </summary>
    public static AssemblyEventModel Create(int assemblyPointId, DateTime assemblyDate, Guid createdBy)
    {
        return new AssemblyEventModel
        {
            AssemblyPointId = assemblyPointId,
            AssemblyDate = assemblyDate,
            Status = AssemblyEventStatus.Gathering,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
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
