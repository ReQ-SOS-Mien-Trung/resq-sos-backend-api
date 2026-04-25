using RESQ.Domain.Entities.Personnel.Exceptions;
using RESQ.Domain.Entities.Personnel.ValueObjects;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Domain.Entities.Personnel;

public class RescueTeamMemberModel
{
    public int TeamId { get; private set; }
    public Guid UserId { get; private set; }
    public TeamMemberStatus Status { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public int? SourceEventId { get; private set; }
    public bool IsLeader { get; private set; }
    public string? RoleInTeam { get; private set; }

    public RescuerProfile? Profile { get; private set; }

    protected RescueTeamMemberModel() { }

    /// <summary>
    /// Tạo member mới - đã check-in tại AP, trạng thái Accepted ngay lập tức.
    /// </summary>
    internal static RescueTeamMemberModel Create(Guid userId, bool isLeader, string rescuerType, string? roleInTeam, int? sourceEventId = null)
    {
        if (isLeader && !string.Equals(rescuerType, "Core", StringComparison.OrdinalIgnoreCase))
            throw new TeamMemberDomainException("Đội trưởng phải là nhân sự nòng cốt (Core Rescuer).");

        return new RescueTeamMemberModel
        {
            UserId = userId,
            Status = TeamMemberStatus.Accepted,
            JoinedAt = DateTime.UtcNow,
            SourceEventId = sourceEventId,
            IsLeader = isLeader,
            RoleInTeam = roleInTeam
        };
    }

    public void LoadProfile(RescuerProfile profile)
    {
        Profile = profile;
    }

    public void Remove()
    {
        Status = TeamMemberStatus.Removed;
    }
}
