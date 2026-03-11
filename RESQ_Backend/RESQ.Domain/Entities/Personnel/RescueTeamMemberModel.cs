using RESQ.Domain.Entities.Personnel.Exceptions;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Domain.Entities.Personnel;

public class RescueTeamMemberModel
{
    public int TeamId { get; private set; }
    public Guid UserId { get; private set; }
    public TeamMemberStatus Status { get; private set; }
    public DateTime InvitedAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    public bool IsLeader { get; private set; }
    public string? RoleInTeam { get; private set; }
    public bool CheckedIn { get; private set; }

    // EF constructor
    protected RescueTeamMemberModel() { }

    internal RescueTeamMemberModel(Guid userId, bool isLeader, string rescuerType, string? roleInTeam)
    {
        if (isLeader && !string.Equals(rescuerType, "Core", StringComparison.OrdinalIgnoreCase))
            throw new TeamMemberDomainException("Đội trưởng phải là nhân sự nòng cốt (Core Rescuer).");

        UserId = userId;
        Status = TeamMemberStatus.Pending;
        InvitedAt = DateTime.UtcNow;
        IsLeader = isLeader;
        RoleInTeam = roleInTeam;
        CheckedIn = false;
    }

    public void Accept()
    {
        if (Status != TeamMemberStatus.Pending)
            throw new TeamMemberDomainException("Chỉ có thể chấp nhận lời mời đang ở trạng thái Pending.");

        if ((DateTime.UtcNow - InvitedAt).TotalHours > 24)
        {
            Status = TeamMemberStatus.Declined;
            RespondedAt = DateTime.UtcNow;
            throw new TeamMemberDomainException("Lời mời đã hết hạn sau 24h và tự động bị từ chối.");
        }

        Status = TeamMemberStatus.Accepted;
        RespondedAt = DateTime.UtcNow;
    }

    public void Decline()
    {
        if (Status != TeamMemberStatus.Pending)
            throw new TeamMemberDomainException("Chỉ có thể từ chối lời mời đang ở trạng thái Pending.");

        Status = TeamMemberStatus.Declined;
        RespondedAt = DateTime.UtcNow;
    }

    public void CheckIn()
    {
        if (Status != TeamMemberStatus.Accepted)
            throw new TeamMemberDomainException("Thành viên phải chấp nhận lời mời trước khi điểm danh.");
        
        CheckedIn = true;
    }

    public void Remove()
    {
        Status = TeamMemberStatus.Removed;
    }
}