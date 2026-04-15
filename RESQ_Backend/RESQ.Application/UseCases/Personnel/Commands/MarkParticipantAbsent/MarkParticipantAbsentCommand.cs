using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.MarkParticipantAbsent;

/// <summary>
/// Đội trưởng đánh dấu một thành viên vắng mặt tại sự kiện tập trung.
/// Ghi nhận trạng thái Absent và thông báo tới coordinator.
/// </summary>
public class MarkParticipantAbsentCommand : IRequest
{
    /// <summary>ID của sự kiện tập trung.</summary>
    public int EventId { get; set; }

    /// <summary>UserId của thành viên bị đánh dấu vắng mặt.</summary>
    public Guid TargetRescuerId { get; set; }

    /// <summary>UserId của đội trưởng đang thực hiện thao tác.</summary>
    public Guid CallerUserId { get; set; }

    public MarkParticipantAbsentCommand(int eventId, Guid targetRescuerId, Guid callerUserId)
    {
        EventId = eventId;
        TargetRescuerId = targetRescuerId;
        CallerUserId = callerUserId;
    }
}
