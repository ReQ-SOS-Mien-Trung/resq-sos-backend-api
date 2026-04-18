namespace RESQ.Domain.Enum.Personnel;

public enum AssemblyParticipantStatus
{
    /// <summary>Được gán vào sự kiện, chưa check-in.</summary>
    Assigned,

    /// <summary>Đã check-in tại điểm tập kết, đang hiện diện.</summary>
    CheckedIn,

    /// <summary>Đã check-out đi làm nhiệm vụ. Có thể trở về và check-in lại qua ReturnCheckIn.</summary>
    CheckedOutForMission,

    /// <summary>Tự rời khỏi sự kiện tập trung. Không thể check-in lại.</summary>
    CheckedOut,

    /// <summary>Không check-in trước deadline hoặc bị đội trưởng đánh dấu vắng mặt.</summary>
    Absent
}
