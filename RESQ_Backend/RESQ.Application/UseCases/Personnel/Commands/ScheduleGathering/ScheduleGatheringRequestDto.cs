namespace RESQ.Application.UseCases.Personnel.Commands.ScheduleGathering;

public class ScheduleGatheringRequestDto
{
    /// <summary>Ngày giờ tập trung (UTC).</summary>
    public DateTime AssemblyDate { get; set; }

    /// <summary>Thời hạn check-in. Phải là thời điểm trong tương lai và trước hoặc bằng ngày giờ tập trung.</summary>
    public DateTime CheckInDeadline { get; set; }
}
